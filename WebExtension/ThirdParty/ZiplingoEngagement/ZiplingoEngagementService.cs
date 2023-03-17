using Newtonsoft.Json;
using System;
using System.Linq;
using WebExtension.ThirdParty.Interfaces;
using WebExtension.ThirdParty.Model;
using Microsoft.Extensions.Logging;
using WebExtension.ThirdParty.ZiplingoEngagement.Interfaces;
using WebExtension.ThirdParty.ZiplingoEngagement.Model;
using DirectScale.Disco.Extension.Services;
using DirectScale.Disco.Extension;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;
using DirectScale.Disco.Extension.Hooks.Commissions;
using WebExtension.Services.ZiplingoEngagement.Model;

using System.Text.RegularExpressions;
using HelloWebExtension.Helper;
using WebExtension.Helper;
using WebExtension.Models;
using WebExtension.Repositories;

namespace WebExtension.ThirdParty
{
    public class ZiplingoEngagementService : IZiplingoEngagementService
    {
        private readonly IZiplingoEngagementRepository _ZiplingoEngagementRepository;
        private readonly ICompanyService _companyService;
        //private readonly ILogger _logger;
        private static readonly string ClassName = typeof(ZiplingoEngagementService).FullName;
        private readonly IOrderService _orderService;
        private readonly IAssociateService _distributorService;
        private readonly ITreeService _treeService;
        private readonly IRankService _rankService;
        private readonly IHttpClientService _httpClientService;
        private readonly IPaymentProcessingService _paymentProcessingService;
        private readonly ICustomLogRepository _customLogRepository;

        public ZiplingoEngagementService(IZiplingoEngagementRepository repository,
            ICompanyService companyService,
          //  ILogger logger,
            IOrderService orderService,
            IAssociateService distributorService,
            ITreeService treeService,
            IRankService rankService,
            IHttpClientService httpClientService,
            IPaymentProcessingService paymentProcessingService,
            ICustomLogRepository customLogRepository
            )
        {
            _ZiplingoEngagementRepository = repository ?? throw new ArgumentNullException(nameof(repository));
            _companyService = companyService ?? throw new ArgumentNullException(nameof(companyService));
            //_logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
            _distributorService = distributorService ?? throw new ArgumentNullException(nameof(distributorService));
            _treeService = treeService ?? throw new ArgumentNullException(nameof(treeService));
            _rankService = rankService ?? throw new ArgumentNullException(nameof(rankService));
            _httpClientService = httpClientService ?? throw new ArgumentNullException(nameof(httpClientService));
            _paymentProcessingService = paymentProcessingService ?? throw new ArgumentNullException(nameof(paymentProcessingService));
            _customLogRepository = customLogRepository;
        }

        public async void CallOrderZiplingoEngagementTrigger(Order order, string eventKey, bool FailedAutoship)
        {
            try
            {
                var company = _companyService.GetCompany();
                var settings = _ZiplingoEngagementRepository.GetSettings();
                int enrollerID = 0;
                int sponsorID = 0;
                if (_treeService.GetNodeDetail(new NodeId(order.AssociateId, 0), TreeType.Enrollment).Result.UplineId != null)
                {
                    enrollerID = _treeService.GetNodeDetail(new NodeId(order.AssociateId, 0), TreeType.Enrollment).Result?.UplineId.AssociateId ?? 0;
                }
                if (_treeService.GetNodeDetail(new NodeId(order.AssociateId, 0), TreeType.Unilevel).Result.UplineId != null)
                {
                    sponsorID = _treeService.GetNodeDetail(new NodeId(order.AssociateId, 0), TreeType.Unilevel)?.Result.UplineId.AssociateId ?? 0;
                }

                Associate sponsorSummary = new Associate();
                Associate enrollerSummary = new Associate();
                if (enrollerID <= 0)
                {
                    enrollerSummary = new Associate();
                }
                else
                {
                    enrollerSummary = await _distributorService.GetAssociate(enrollerID);
                }
                if (sponsorID > 0)
                {
                    sponsorSummary = await _distributorService.GetAssociate(sponsorID);
                }
                else
                {
                    sponsorSummary = enrollerSummary;
                }
                var CardLastFourDegit = _ZiplingoEngagementRepository.GetLastFoutDegitByOrderNumber(order.OrderNumber);
                OrderData data = new OrderData
                {
                    ShipMethodId = order.Packages.Select(a => a.ShipMethodId).FirstOrDefault(),
                    AssociateId = order.AssociateId,
                    BackofficeId = order.BackofficeId,
                    Email = order.Email,
                    InvoiceDate = order.InvoiceDate,
                    IsPaid = order.IsPaid,
                    LocalInvoiceNumber = order.LocalInvoiceNumber,
                    Name = order.Name,
                    Phone = order.BillPhone,
                    OrderDate = order.OrderDate,
                    OrderNumber = order.OrderNumber,
                    OrderType = order.OrderType,
                    Tax = order.Totals.Select(m => m.Tax).FirstOrDefault(),
                    ShipCost = order.Totals.Select(m => m.Shipping).FirstOrDefault(),
                    Subtotal = order.Totals.Select(m => m.SubTotal).FirstOrDefault(),
                    Total = order.Totals.Select(m => m.Total).FirstOrDefault(),
                    Discount = order.Totals.Select(m => m.DiscountTotal).FirstOrDefault(),
                    CurrencySymbol = order.Totals.Select(m => m.CurrencySymbol).FirstOrDefault(),
                    PaymentMethod = CardLastFourDegit,
                    ProductInfo = order.LineItems,
                    ProductNames = string.Join(",", order.LineItems.Select(x => x.ProductName).ToArray()),
                    ErrorDetails = FailedAutoship ? order.Payments.FirstOrDefault().PaymentResponse.ToString() : "",
                    CompanyDomain = company.Result.BackOfficeHomePageURL,
                    LogoUrl = settings.LogoUrl,
                    CompanyName = settings.CompanyName,
                    EnrollerId = enrollerSummary.AssociateId,
                    SponsorId = sponsorSummary.AssociateId,
                    EnrollerName = enrollerSummary.DisplayFirstName + ' ' + enrollerSummary.DisplayLastName,
                    EnrollerMobile = enrollerSummary.PrimaryPhone,
                    EnrollerEmail = enrollerSummary.EmailAddress,
                    SponsorName = sponsorSummary.DisplayFirstName + ' ' + sponsorSummary.DisplayLastName,
                    SponsorMobile = sponsorSummary.PrimaryPhone,
                    SponsorEmail = sponsorSummary.EmailAddress,
                    BillingAddress = order.BillAddress,
                    ShippingAddress = order.Packages?.FirstOrDefault()?.ShippingAddress
                };
                var strData = JsonConvert.SerializeObject(data);
                ZiplingoEngagementRequest request = new ZiplingoEngagementRequest { associateid = order.AssociateId, companyname = settings.CompanyName, eventKey = eventKey, data = strData };
                var jsonReq = JsonConvert.SerializeObject(request);
                CallZiplingoEngagementApi(jsonReq, "Campaign/ExecuteTrigger");
            }
            catch (Exception e)
            {
                //_logger.LogError($"{ClassName}.CallOrderZiplingoEngagementTrigger", $"Exception occurred attempting to Execute CallOrderZiplingoEngagementTrigger for associate {order.AssociateId}", e);
            }
        }

        public async void CallOrderZiplingoEngagementTriggerForShipped(OrderDetailModel order, string eventKey, bool FailedAutoship = false)
        {
            try
            {
                var company = _companyService.GetCompany();
                var settings = _ZiplingoEngagementRepository.GetSettings();
                int enrollerID = 0;
                int sponsorID = 0;
                if (_treeService.GetNodeDetail(new NodeId(order.Order.AssociateId, 0), TreeType.Enrollment).Result.UplineId != null)
                {
                    enrollerID = _treeService.GetNodeDetail(new NodeId(order.Order.AssociateId, 0), TreeType.Enrollment)?.Result.UplineId.AssociateId ?? 0;
                }
                if (_treeService.GetNodeDetail(new NodeId(order.Order.AssociateId, 0), TreeType.Unilevel).Result.UplineId != null)
                {
                    sponsorID = _treeService.GetNodeDetail(new NodeId(order.Order.AssociateId, 0), TreeType.Unilevel)?.Result.UplineId.AssociateId ?? 0;
                }

                Associate sponsorSummary = new Associate();
                Associate enrollerSummary = new Associate();
                if (enrollerID <= 0)
                {
                    enrollerSummary = new Associate();
                }
                else
                {
                    enrollerSummary = await _distributorService.GetAssociate(enrollerID);
                }
                if (sponsorID > 0)
                {
                    sponsorSummary = await _distributorService.GetAssociate(sponsorID);
                }
                else
                {
                    sponsorSummary = enrollerSummary;
                }
                var CardLastFourDegit = _ZiplingoEngagementRepository.GetLastFoutDegitByOrderNumber(order.Order.OrderNumber);

                // Track Shipping -----------------------------
                var TrackingUrl = "";
                var ShippingTrackingInfo = _ZiplingoEngagementRepository.GetShippingTrackingInfo();
                if (order.TrackingNumber != null)
                {
                    foreach (var shipInfo in ShippingTrackingInfo)
                    {
                        Match m1 = Regex.Match(order.TrackingNumber, shipInfo.TrackPattern, RegexOptions.IgnoreCase);
                        if (m1.Success)
                        {
                            TrackingUrl = shipInfo.ShippingUrl + order.TrackingNumber;
                            break;
                        }
                    }
                }

                OrderData data = new OrderData
                {
                    ShipMethodId = order.ShipMethodId, //ShipMethodId added
                    AssociateId = order.Order.AssociateId,
                    BackofficeId = order.Order.BackofficeId,
                    Email = order.Order.Email,
                    InvoiceDate = order.Order.InvoiceDate,
                    IsPaid = order.Order.IsPaid,
                    LocalInvoiceNumber = order.Order.LocalInvoiceNumber,
                    Name = order.Order.Name,
                    Phone = order.Order.BillPhone,
                    OrderDate = order.Order.OrderDate,
                    OrderNumber = order.Order.OrderNumber,
                    OrderType = order.Order.OrderType,
                    Tax = order.Order.Totals.Select(m => m.Tax).FirstOrDefault(),
                    ShipCost = order.Order.Totals.Select(m => m.Shipping).FirstOrDefault(),
                    Subtotal = order.Order.Totals.Select(m => m.SubTotal).FirstOrDefault(),
                    Total = order.Order.Totals.Select(m => m.Total).FirstOrDefault(),
                    CurrencySymbol = order.Order.Totals.Select(m => m.CurrencySymbol).FirstOrDefault(),
                    PaymentMethod = CardLastFourDegit,
                    ProductInfo = order.Order.LineItems,
                    ProductNames = string.Join(",", order.Order.LineItems.Select(x => x.ProductName).ToArray()),
                    ErrorDetails = FailedAutoship ? order.Order.Payments.FirstOrDefault().PaymentResponse.ToString() : "",
                    CompanyDomain = company.Result.BackOfficeHomePageURL,
                    LogoUrl = settings.LogoUrl,
                    TrackingNumber = order.TrackingNumber,
                    TrackingUrl= TrackingUrl,
                    Carrier = order.Carrier,
                    DateShipped = order.DateShipped,
                    CompanyName = settings.CompanyName,
                    EnrollerId = enrollerSummary.AssociateId,
                    SponsorId = sponsorSummary.AssociateId,
                    AutoshipId = order.AutoshipId,
                    EnrollerName = enrollerSummary.DisplayFirstName + ' ' + enrollerSummary.DisplayLastName,
                    EnrollerMobile = enrollerSummary.PrimaryPhone,
                    EnrollerEmail = enrollerSummary.EmailAddress,
                    SponsorName = sponsorSummary.DisplayFirstName + ' ' + sponsorSummary.DisplayLastName,
                    SponsorMobile = sponsorSummary.PrimaryPhone,
                    SponsorEmail = sponsorSummary.EmailAddress,
                    BillingAddress = order.Order.BillAddress,
                    ShippingAddress = order.Order.Packages?.FirstOrDefault()?.ShippingAddress
                };
                var strData = JsonConvert.SerializeObject(data);
                ZiplingoEngagementRequest request = new ZiplingoEngagementRequest { associateid = order.Order.AssociateId, companyname = settings.CompanyName, eventKey = eventKey, data = strData };
                var jsonReq = JsonConvert.SerializeObject(request);
                CallZiplingoEngagementApi(jsonReq, "Campaign/ExecuteTrigger");
            }
            catch (Exception e)
            {
                //_logger.LogError($"{ClassName}.CallOrderZiplingoEngagementTrigger", $"Exception occurred attempting to Execute CallOrderZiplingoEngagementTrigger for associate {order.Order.AssociateId}", e);
            }
        }

        public async void CallOrderZiplingoEngagementTriggerForBirthDayWishes(AssociateInfo assoInfo, string eventKey)
        {
            try
            {
                var company = _companyService.GetCompany();
                var settings = _ZiplingoEngagementRepository.GetSettings();
                int enrollerID = 0;
                int sponsorID = 0;
                if (_treeService.GetNodeDetail(new NodeId(assoInfo.AssociateId, 0), TreeType.Enrollment).Result.UplineId != null)
                {
                    enrollerID = _treeService.GetNodeDetail(new NodeId(assoInfo.AssociateId, 0), TreeType.Enrollment)?.Result.UplineId.AssociateId ?? 0;
                }
                if (_treeService.GetNodeDetail(new NodeId(assoInfo.AssociateId, 0), TreeType.Unilevel).Result.UplineId != null)
                {
                    sponsorID = _treeService.GetNodeDetail(new NodeId(assoInfo.AssociateId, 0), TreeType.Unilevel)?.Result.UplineId.AssociateId ?? 0;
                }

                Associate sponsorSummary = new Associate();
                Associate enrollerSummary = new Associate();
                if (enrollerID <= 0)
                {
                    enrollerSummary = new Associate();
                }
                else
                {
                    enrollerSummary = await _distributorService.GetAssociate(enrollerID);
                }
                if (sponsorID > 0)
                {
                    sponsorSummary = await _distributorService.GetAssociate(sponsorID);
                }
                else
                {
                    sponsorSummary = enrollerSummary;
                }
                AssociateInfo data = new AssociateInfo
                {
                    AssociateId = assoInfo.AssociateId,
                    EmailAddress = assoInfo.EmailAddress,
                    Birthdate = assoInfo.Birthdate,
                    FirstName = assoInfo.FirstName,
                    LastName = assoInfo.LastName,
                    CompanyDomain = company.Result.BackOfficeHomePageURL,
                    LogoUrl = settings.LogoUrl,
                    CompanyName = settings.CompanyName,
                    EnrollerId = enrollerSummary.AssociateId,
                    SponsorId = sponsorSummary.AssociateId,
                    CommissionActive = true,
                    EnrollerName = enrollerSummary.DisplayFirstName + ' ' + enrollerSummary.DisplayLastName,
                    EnrollerMobile = enrollerSummary.PrimaryPhone,
                    EnrollerEmail = enrollerSummary.EmailAddress,
                    SponsorName = sponsorSummary.DisplayFirstName + ' ' + sponsorSummary.DisplayLastName,
                    SponsorMobile = sponsorSummary.PrimaryPhone,
                    SponsorEmail = sponsorSummary.EmailAddress
                };
                var strData = JsonConvert.SerializeObject(data);
                ZiplingoEngagementRequest request = new ZiplingoEngagementRequest { associateid = assoInfo.AssociateId, companyname = settings.CompanyName, eventKey = eventKey, data = strData };
                var jsonReq = JsonConvert.SerializeObject(request);
                CallZiplingoEngagementApi(jsonReq, "Campaign/ExecuteTrigger");
            }
            catch (Exception e)
            {
                //_logger.LogError($"{ClassName}.CallOrderZiplingoEngagementTriggerForBirthDayWishes", $"Exception occurred attempting to Execute CallOrderZiplingoEngagementTriggerForBirthDayWishes for associate {assoInfo.AssociateId}", e);
            }
        }

        public async void CallOrderZiplingoEngagementTriggerForWorkAnniversary(AssociateInfo assoInfo, string eventKey)
        {
            try
            {
                var company = _companyService.GetCompany();
                var settings = _ZiplingoEngagementRepository.GetSettings();
                int enrollerID = 0;
                int sponsorID = 0;
                if (_treeService.GetNodeDetail(new NodeId(assoInfo.AssociateId, 0), TreeType.Enrollment).Result.UplineId != null)
                {
                    enrollerID = _treeService.GetNodeDetail(new NodeId(assoInfo.AssociateId, 0), TreeType.Enrollment)?.Result.UplineId.AssociateId ?? 0;
                }
                if (_treeService.GetNodeDetail(new NodeId(assoInfo.AssociateId, 0), TreeType.Unilevel).Result.UplineId != null)
                {
                    sponsorID = _treeService.GetNodeDetail(new NodeId(assoInfo.AssociateId, 0), TreeType.Unilevel)?.Result.UplineId.AssociateId ?? 0;
                }

                Associate sponsorSummary = new Associate();
                Associate enrollerSummary = new Associate();
                if (enrollerID <= 0)
                {
                    enrollerSummary = new Associate();
                }
                else
                {
                    enrollerSummary = await _distributorService.GetAssociate(enrollerID);
                }
                if (sponsorID > 0)
                {
                    sponsorSummary = await _distributorService.GetAssociate(sponsorID);
                }
                else
                {
                    sponsorSummary = enrollerSummary;
                }
                AssociateInfo data = new AssociateInfo
                {
                    AssociateId = assoInfo.AssociateId,
                    EmailAddress = assoInfo.EmailAddress,
                    SignupDate = assoInfo.SignupDate,
                    TotalWorkingYears = assoInfo.TotalWorkingYears,
                    FirstName = assoInfo.FirstName,
                    LastName = assoInfo.LastName,
                    CompanyDomain = company.Result.BackOfficeHomePageURL,
                    LogoUrl = settings.LogoUrl,
                    CompanyName = settings.CompanyName,
                    EnrollerId = enrollerSummary.AssociateId,
                    SponsorId = sponsorSummary.AssociateId,
                    CommissionActive = true,
                    EnrollerName = enrollerSummary.DisplayFirstName + ' ' + enrollerSummary.DisplayLastName,
                    EnrollerMobile = enrollerSummary.PrimaryPhone,
                    EnrollerEmail = enrollerSummary.EmailAddress,
                    SponsorName = sponsorSummary.DisplayFirstName + ' ' + sponsorSummary.DisplayLastName,
                    SponsorMobile = sponsorSummary.PrimaryPhone,
                    SponsorEmail = sponsorSummary.EmailAddress
                };
                var strData = JsonConvert.SerializeObject(data);
                ZiplingoEngagementRequest request = new ZiplingoEngagementRequest { associateid = assoInfo.AssociateId, companyname = settings.CompanyName, eventKey = eventKey, data = strData };
                var jsonReq = JsonConvert.SerializeObject(request);
                CallZiplingoEngagementApi(jsonReq, "Campaign/ExecuteTrigger");
            }
            catch (Exception e)
            {
                //_logger.LogError($"{ClassName}.CallOrderZiplingoEngagementTriggerForWorkAnniversary", $"Exception occurred attempting to Execute CallOrderZiplingoEngagementTriggerForWorkAnniversary for associate {assoInfo.AssociateId}", e);
            }
        }

        public async void CallOrderZiplingoEngagementTriggerForAssociateRankAdvancement(AssociateRankAdvancement assoRankAdvancementInfo, string eventKey)
        {
            try
            {
                var company = _companyService.GetCompany();
                var settings = _ZiplingoEngagementRepository.GetSettings();
                int enrollerID = 0;
                int sponsorID = 0;
                if (_treeService.GetNodeDetail(new NodeId(assoRankAdvancementInfo.AssociateId, 0), TreeType.Enrollment).Result.UplineId != null)
                {
                    enrollerID = _treeService.GetNodeDetail(new NodeId(assoRankAdvancementInfo.AssociateId, 0), TreeType.Enrollment)?.Result.UplineId.AssociateId ?? 0;
                }
                if (_treeService.GetNodeDetail(new NodeId(assoRankAdvancementInfo.AssociateId, 0), TreeType.Unilevel).Result.UplineId != null)
                {
                    sponsorID = _treeService.GetNodeDetail(new NodeId(assoRankAdvancementInfo.AssociateId, 0), TreeType.Unilevel)?.Result.UplineId.AssociateId ?? 0;
                }

                Associate sponsorSummary = new Associate();
                Associate enrollerSummary = new Associate();
                if (enrollerID <= 0)
                {
                    enrollerSummary = new Associate();
                }
                else
                {
                    enrollerSummary = await _distributorService.GetAssociate(enrollerID);
                }
                if (sponsorID > 0)
                {
                    sponsorSummary = await _distributorService.GetAssociate(sponsorID);
                }
                else
                {
                    sponsorSummary = enrollerSummary;
                }
                var associateSummary = _distributorService.GetAssociate(assoRankAdvancementInfo.AssociateId);
                AssociateRankAdvancement data = new AssociateRankAdvancement
                {
                    Rank = assoRankAdvancementInfo.Rank,
                    AssociateId = assoRankAdvancementInfo.AssociateId,
                    FirstName = assoRankAdvancementInfo.FirstName,
                    LastName = assoRankAdvancementInfo.LastName,
                    EmailAddress = associateSummary.Result.EmailAddress,
                    PrimaryPhone = associateSummary.Result.PrimaryPhone,
                    CompanyDomain = company.Result.BackOfficeHomePageURL,
                    LogoUrl = settings.LogoUrl,
                    CompanyName = settings.CompanyName,
                    EnrollerId = enrollerSummary.AssociateId,
                    SponsorId = sponsorSummary.AssociateId,
                    RankName = assoRankAdvancementInfo.RankName,
                    CommissionActive = true,
                    EnrollerName = enrollerSummary.DisplayFirstName + ' ' + enrollerSummary.DisplayLastName,
                    EnrollerMobile = enrollerSummary.PrimaryPhone,
                    EnrollerEmail = enrollerSummary.EmailAddress,
                    SponsorName = sponsorSummary.DisplayFirstName + ' ' + sponsorSummary.DisplayLastName,
                    SponsorMobile = sponsorSummary.PrimaryPhone,
                    SponsorEmail = sponsorSummary.EmailAddress
                };
                var strData = JsonConvert.SerializeObject(data);
				_customLogRepository.SaveLog(data.AssociateId, 0, "Debug log : Rank Advancement", "Before trigger API call", "", "", "", "", "");
				ZiplingoEngagementRequest request = new ZiplingoEngagementRequest { associateid = assoRankAdvancementInfo.AssociateId, companyname = settings.CompanyName, eventKey = eventKey, data = strData, rankid = assoRankAdvancementInfo.Rank };
                var jsonReq = JsonConvert.SerializeObject(request);
                CallZiplingoEngagementApi(jsonReq, "Campaign/RankAdvancement");
            }
            catch (Exception e)
            {
                //_logger.LogError($"{ClassName}.CallOrderZiplingoEngagementTriggerForAssociateRankAdvancement", $"Exception occurred attempting to Execute CallOrderZiplingoEngagementTriggerForAssociateRankAdvancement for associate {assoRankAdvancementInfo.AssociateId}", e);
            }
        }

        public async void CallOrderZiplingoEngagementTriggerForAssociateChangeStatus(AssociateStatusChange assoStatusChangeInfo, string eventKey)
        {
            try
            {
                var company = _companyService.GetCompany();
                var settings = _ZiplingoEngagementRepository.GetSettings();
                var UserName = _ZiplingoEngagementRepository.GetUsernameById(Convert.ToString(assoStatusChangeInfo.AssociateId));
                int enrollerID = 0;
                int sponsorID = 0;
                if (_treeService.GetNodeDetail(new NodeId(assoStatusChangeInfo.AssociateId, 0), TreeType.Enrollment).Result.UplineId != null)
                {
                    enrollerID = _treeService.GetNodeDetail(new NodeId(assoStatusChangeInfo.AssociateId, 0), TreeType.Enrollment)?.Result.UplineId.AssociateId ?? 0;
                }
                if (_treeService.GetNodeDetail(new NodeId(assoStatusChangeInfo.AssociateId, 0), TreeType.Unilevel).Result.UplineId != null)
                {
                    sponsorID = _treeService.GetNodeDetail(new NodeId(assoStatusChangeInfo.AssociateId, 0), TreeType.Unilevel)?.Result.UplineId.AssociateId ?? 0;
                }

                Associate sponsorSummary = new Associate();
                Associate enrollerSummary = new Associate();
                if (enrollerID <= 0)
                {
                    enrollerSummary = new Associate();
                }
                else
                {
                    enrollerSummary = await _distributorService.GetAssociate(enrollerID);
                }
                if (sponsorID > 0)
                {
                    sponsorSummary = await _distributorService.GetAssociate(sponsorID);
                }
                else
                {
                    sponsorSummary = enrollerSummary;
                }
                AssociateStatusChange data = new AssociateStatusChange
                {
                    OldStatusId= assoStatusChangeInfo.OldStatusId,
                    OldStatus = assoStatusChangeInfo.OldStatus,
                    NewStatusId = assoStatusChangeInfo.NewStatusId,
                    NewStatus = assoStatusChangeInfo.NewStatus,
                    AssociateId = assoStatusChangeInfo.AssociateId,
                    FirstName = assoStatusChangeInfo.FirstName,
                    LastName = assoStatusChangeInfo.LastName,
                    CompanyDomain = company.Result.BackOfficeHomePageURL,
                    LogoUrl = settings.LogoUrl,
                    CompanyName = settings.CompanyName,
                    EnrollerId = enrollerSummary.AssociateId,
                    SponsorId = sponsorSummary.AssociateId,
                    EnrollerName = enrollerSummary.DisplayFirstName + ' ' + enrollerSummary.DisplayLastName,
                    EnrollerMobile = enrollerSummary.PrimaryPhone,
                    EnrollerEmail = enrollerSummary.EmailAddress,
                    SponsorName = sponsorSummary.DisplayFirstName + ' ' + sponsorSummary.DisplayLastName,
                    SponsorMobile = sponsorSummary.PrimaryPhone,
                    SponsorEmail = sponsorSummary.EmailAddress,
                    EmailAddress = assoStatusChangeInfo.EmailAddress,
                    WebAlias = UserName
                };
                var strData = JsonConvert.SerializeObject(data);
                ZiplingoEngagementRequest request = new ZiplingoEngagementRequest { associateid = assoStatusChangeInfo.AssociateId, companyname = settings.CompanyName, eventKey = eventKey, data = strData, associateStatus = assoStatusChangeInfo.NewStatusId };
                var jsonReq = JsonConvert.SerializeObject(request);
                CallZiplingoEngagementApi(jsonReq, "Campaign/ChangeAssociateStatus");
            }
            catch (Exception e)
            {
                //_logger.LogError($"{ClassName}.CallOrderZiplingoEngagementTriggerForAssociateChangeStatus", $"Exception occurred attempting to Execute CallOrderZiplingoEngagementTriggerForAssociateChangeStatus for associate {assoStatusChangeInfo.AssociateId}", e);
            }
        }

        public async void CreateEnrollContact(Order order)
        {
            try
            {
                var company = await _companyService.GetCompany();
                var associateInfo = await _distributorService.GetAssociate(order.AssociateId);
                var settings = _ZiplingoEngagementRepository.GetSettings();
                var UserName = _ZiplingoEngagementRepository.GetUsernameById(Convert.ToString(order.AssociateId));
                int enrollerID = 0;
                int sponsorID = 0;
                if (_treeService.GetNodeDetail(new NodeId(order.AssociateId, 0), TreeType.Enrollment).Result.UplineId != null)
                {
                    enrollerID = _treeService.GetNodeDetail(new NodeId(order.AssociateId, 0), TreeType.Enrollment)?.Result.UplineId.AssociateId ?? 0;
                }
                if (_treeService.GetNodeDetail(new NodeId(order.AssociateId, 0), TreeType.Unilevel).Result.UplineId != null)
                {
                    sponsorID = _treeService.GetNodeDetail(new NodeId(order.AssociateId, 0), TreeType.Unilevel)?.Result.UplineId.AssociateId ?? 0;
                }

                Associate sponsorSummary = new Associate();
                Associate enrollerSummary = new Associate();
                if (enrollerID <= 0)
                {
                    enrollerSummary = new Associate();
                }
                else
                {
                    enrollerSummary = await _distributorService.GetAssociate(enrollerID);
                }
                if (sponsorID > 0)
                {
                    sponsorSummary = await _distributorService.GetAssociate(sponsorID);
                }
                else
                {
                    sponsorSummary = enrollerSummary;
                }
                var ZiplingoEngagementRequest = new AssociateContactModel
                {
                    AssociateId = associateInfo.AssociateId,
                    AssociateType = associateInfo.AssociateBaseType,
                    BackOfficeId = associateInfo.BackOfficeId,
                    firstName = associateInfo.DisplayFirstName,
                    lastName = associateInfo.DisplayLastName,
                    address = associateInfo.Address.AddressLine1 + " " + associateInfo.Address.AddressLine2 + " " + associateInfo.Address.AddressLine3,
                    city = associateInfo.Address.City,
                    birthday = associateInfo.BirthDate,
                    OrderDate = order.OrderDate, // OrderDate added for first order
                    CountryCode = associateInfo.Address.CountryCode,
                    distributerId = associateInfo.BackOfficeId,
                    phoneNumber = associateInfo.TextNumber,
                    region = associateInfo.Address.CountryCode,
                    state = associateInfo.Address.State,
                    zip = associateInfo.Address.PostalCode,
                    UserName = UserName,
                    WebAlias = UserName,
                    CompanyUrl = company.BackOfficeHomePageURL,
                    CompanyDomain = company.BackOfficeHomePageURL,
                    LanguageCode = associateInfo.LanguageCode,
                    CommissionActive = true,
                    emailAddress = associateInfo.EmailAddress,
                    CompanyName = settings.CompanyName,
                    EnrollerId = enrollerSummary.AssociateId,
                    SponsorId = sponsorSummary.AssociateId,
                    EnrollerName = enrollerSummary.DisplayFirstName + ' ' + enrollerSummary.DisplayLastName,
                    EnrollerMobile = enrollerSummary.PrimaryPhone,
                    EnrollerEmail = enrollerSummary.EmailAddress,
                    SponsorName = sponsorSummary.DisplayFirstName + ' ' + sponsorSummary.DisplayLastName,
                    SponsorMobile = sponsorSummary.PrimaryPhone,
                    SponsorEmail = sponsorSummary.EmailAddress,
                    JoinDate = associateInfo.SignupDate.ToUniversalTime(),
                    ActiveAutoship =   _orderService.GetOrders(new int[]{ order.AssociateId }).Result.Where(o => o.OrderType == OrderType.Autoship).Any()
                };

                var jsonZiplingoEngagementRequest = JsonConvert.SerializeObject(ZiplingoEngagementRequest);
                CallZiplingoEngagementApi(jsonZiplingoEngagementRequest, "Contact/CreateContactV2");
            }
            catch (Exception e)
            {
                //_logger.LogError($"{ClassName}.CreateContact", $"Exception occurred at Execute CreateContact ZiplingoEngagement for associate {order.AssociateId}", e);
            }
        }

        public void AssociateStatusChangeTrigger(int associateId, int oldStatusId, int newStatusId)
        {
            try
            {
                    AssociateStatusChange obj = new AssociateStatusChange();
                    var  distributorInfo = _distributorService.GetAssociate(associateId);
                    obj.OldStatusId = oldStatusId;
                    obj.OldStatus = _ZiplingoEngagementRepository.GetStatusById(oldStatusId);
                    obj.NewStatusId = newStatusId;
                    obj.NewStatus = _ZiplingoEngagementRepository.GetStatusById(newStatusId);
                    obj.AssociateId = associateId;
                    obj.FirstName = distributorInfo.Result.DisplayFirstName;
                    obj.LastName = distributorInfo.Result.DisplayLastName;
                    obj.EmailAddress = distributorInfo.Result.EmailAddress;
                    CallOrderZiplingoEngagementTriggerForAssociateChangeStatus(obj, "ChangeAssociateStatus");
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Exception occurred in Associate Change Process");
            }
        }

        public async void CreateContact(Application req, ApplicationResponse response)
        {
            try
            {
                if (req.AssociateId == 0)
                    req.AssociateId = response.AssociateId;

                if (string.IsNullOrEmpty(req.BackOfficeId))
                    req.BackOfficeId = response.BackOfficeId;

                var  company = await _companyService.GetCompany();
                var settings = _ZiplingoEngagementRepository.GetSettings();
                int enrollerID = 0;
                int sponsorID = 0;
                if (_treeService.GetNodeDetail(new NodeId(req.AssociateId, 0), TreeType.Enrollment).Result.UplineId != null)
                {
                    enrollerID = _treeService.GetNodeDetail(new NodeId(req.AssociateId, 0), TreeType.Enrollment)?.Result.UplineId.AssociateId ?? 0;
                }
                if (_treeService.GetNodeDetail(new NodeId(req.AssociateId, 0), TreeType.Unilevel).Result.UplineId != null)
                {
                    sponsorID = _treeService.GetNodeDetail(new NodeId(req.AssociateId, 0), TreeType.Unilevel)?.Result.UplineId.AssociateId ?? 0;
                }

                Associate sponsorSummary = new Associate();
                Associate enrollerSummary = new Associate();
                if (enrollerID <= 0)
                {
                    enrollerSummary = new Associate();
                }
                else
                {
                    enrollerSummary = await _distributorService.GetAssociate(enrollerID);
                }
                if (sponsorID > 0)
                {
                    sponsorSummary = await _distributorService.GetAssociate(sponsorID);
                }
                else
                {
                    sponsorSummary = enrollerSummary;
                }
                var associateSummary = await _distributorService.GetAssociate(req.AssociateId);
                var ZiplingoEngagementRequest = new AssociateContactModel
                {
                    AssociateId = req.AssociateId,
                    AssociateStatus = req.StatusId,
                    AssociateType = req.AssociateBaseType,
                    BackOfficeId = req.BackOfficeId,
                    birthday = req.BirthDate,
                    address = req.ApplicantAddress.AddressLine1 + " " + req.ApplicantAddress.AddressLine2 + " " + req.ApplicantAddress.AddressLine3,
                    city = req.ApplicantAddress.City,
                    CommissionActive = true,
                    CountryCode = req.ApplicantAddress.CountryCode,
                    distributerId = req.BackOfficeId,
                    emailAddress = req.EmailAddress,
                    firstName = req.FirstName,
                    lastName = req.LastName,
                    phoneNumber = string.IsNullOrEmpty(req.TextNumber) ? req.PrimaryPhone : req.TextNumber,
                    region = req.ApplicantAddress.CountryCode,
                    state = req.ApplicantAddress.State,
                    zip = req.ApplicantAddress.PostalCode,
                    UserName = req.Username,
                    WebAlias = req.Username,
                    CompanyUrl = company.BackOfficeHomePageURL,
                    CompanyDomain = company.BackOfficeHomePageURL,
                    LanguageCode = req.LanguageCode,
                    CompanyName = settings.CompanyName,
                    EnrollerId = enrollerSummary.AssociateId,
                    SponsorId = sponsorSummary.AssociateId,
                    EnrollerName = enrollerSummary.DisplayFirstName + ' ' + enrollerSummary.DisplayLastName,
                    EnrollerMobile = enrollerSummary.PrimaryPhone,
                    EnrollerEmail = enrollerSummary.EmailAddress,
                    SponsorName = sponsorSummary.DisplayFirstName + ' ' + sponsorSummary.DisplayLastName,
                    SponsorMobile = sponsorSummary.PrimaryPhone,
                    SponsorEmail = sponsorSummary.EmailAddress,
                    JoinDate = associateSummary.SignupDate.ToUniversalTime(),
                    ActiveAutoship = _orderService.GetOrders(new []{ req.AssociateId }).Result.Where(o => o.OrderType == OrderType.Autoship).Any()
                };

                var jsonZiplingoEngagementRequest = JsonConvert.SerializeObject(ZiplingoEngagementRequest);
                CallZiplingoEngagementApi(jsonZiplingoEngagementRequest, "Contact/CreateContactV2");
                ZiplingoEngagementRequest request = new ZiplingoEngagementRequest();
               
                     request = new ZiplingoEngagementRequest { associateid = req.AssociateId, companyname = settings.CompanyName, eventKey = "Enrollment", data = jsonZiplingoEngagementRequest };
               
                var jsonReq = JsonConvert.SerializeObject(request);
                CallZiplingoEngagementApi(jsonReq, "Campaign/ExecuteTrigger");
            }
            catch (Exception e)
            {
                //_logger.LogError($"{ClassName}.CreateContact", $"Exception occurred at Execute CreateContact ZiplingoEngagement for associate {req.AssociateId}", e);
            }
        }

        public void SentTRCPointNotification(int orderId, double orderAmount, string emailAddress, string associateId, string description , string orderdate)
        {
            try
            {
                var company = _companyService.GetCompany();
                var settings = _ZiplingoEngagementRepository.GetSettings();
                var  associateInfo = _distributorService.GetAssociate(Convert.ToInt32(associateId));
                var UserName = _ZiplingoEngagementRepository.GetUsernameById(associateId);
                var data = new TRCPointNotificationModel
                {
                    AssociateId = associateId,
                    firstName = associateInfo.Result.DisplayFirstName,
                    lastName = associateInfo.Result.DisplayLastName,
                    CompanyName = settings.CompanyName,
                    WebAlias = UserName,
                    LogoUrl = settings.LogoUrl,
                    CompanyUrl = company.Result.BackOfficeHomePageURL,
                    OrderId = orderId,
                    OrderAmount = orderAmount, 
                    EmailAddress = emailAddress,
                    Description = description,
                    OrderCreated = orderdate
                };
                var strData = JsonConvert.SerializeObject(data);
                ZiplingoEngagementRequest request = new ZiplingoEngagementRequest { associateid = Convert.ToInt32(associateId), companyname = settings.CompanyName, eventKey = "TRCPointNotification", data = strData };
                var jsonReq = JsonConvert.SerializeObject(request);
                CallZiplingoEngagementApi(jsonReq, "Campaign/ExecuteTrigger");
            }
        catch (Exception e)
        {
            //_logger.LogError($"Exception occurred at Execute TRC Notification for associate {associateId}", e);
        }
    }

        public async void UpdateContact(Associate req)
        {
            try
            {
                var settings = _ZiplingoEngagementRepository.GetSettings();
                var company = await _companyService.GetCompany();
                var UserName = _ZiplingoEngagementRepository.GetUsernameById(Convert.ToString(req.AssociateId));
                var  AssociateInfo  = await   _distributorService.GetAssociate(req.AssociateId);
                int enrollerID = 0;
                int sponsorID = 0;
                if (_treeService.GetNodeDetail(new NodeId(req.AssociateId, 0), TreeType.Enrollment).Result.UplineId != null)
                {
                    enrollerID = _treeService.GetNodeDetail(new NodeId(req.AssociateId, 0), TreeType.Enrollment)?.Result.UplineId.AssociateId ?? 0;
                }
                if (_treeService.GetNodeDetail(new NodeId(req.AssociateId, 0), TreeType.Unilevel).Result.UplineId != null)
                {
                    sponsorID = _treeService.GetNodeDetail(new NodeId(req.AssociateId, 0), TreeType.Unilevel)?.Result.UplineId.AssociateId ?? 0;
                }

                Associate   sponsorSummary = new Associate();
                Associate   enrollerSummary  = new Associate();
                if (enrollerID <= 0)
                {
                    enrollerSummary = new Associate();
                }
                else
                {
                    enrollerSummary = await   _distributorService.GetAssociate(enrollerID);
                }
                if (sponsorID > 0)
                {
                    sponsorSummary = await  _distributorService.GetAssociate(sponsorID);
                }
                else
                {
                    sponsorSummary = enrollerSummary;
                }
                var ZiplingoEngagementRequest = new AssociateContactModel
                {
                    AssociateId = AssociateInfo.AssociateId,
                    AssociateType = AssociateInfo.AssociateBaseType,
                    BackOfficeId = AssociateInfo.BackOfficeId,
                    birthday = AssociateInfo.BirthDate,
                    address = AssociateInfo.Address.AddressLine1 + " " + AssociateInfo.Address.AddressLine2 + " " + AssociateInfo.Address.AddressLine3,
                    city = AssociateInfo.Address.City,
                    CommissionActive = true,
                    CountryCode = AssociateInfo.Address.CountryCode,
                    distributerId = AssociateInfo.BackOfficeId,
                    emailAddress = AssociateInfo.EmailAddress,
                    firstName = AssociateInfo.DisplayFirstName,
                    lastName = AssociateInfo.DisplayLastName,
                    phoneNumber =string.IsNullOrEmpty(AssociateInfo.TextNumber) ? AssociateInfo.PrimaryPhone : AssociateInfo.TextNumber,
                    region = AssociateInfo.Address.CountryCode,
                    state = AssociateInfo.Address.State,
                    zip = AssociateInfo.Address.PostalCode,
                    LanguageCode = AssociateInfo.LanguageCode,
                    UserName = UserName,
                    WebAlias = UserName,
                    CompanyUrl = company.BackOfficeHomePageURL,
                    CompanyDomain = company.BackOfficeHomePageURL,
                    CompanyName = settings.CompanyName,
                    EnrollerId = enrollerSummary.AssociateId,
                    SponsorId = sponsorSummary.AssociateId,
                    EnrollerName = enrollerSummary.DisplayFirstName + ' ' + enrollerSummary.DisplayLastName,
                    EnrollerMobile = enrollerSummary.PrimaryPhone,
                    EnrollerEmail = enrollerSummary.EmailAddress,
                    SponsorName = sponsorSummary.DisplayFirstName + ' ' + sponsorSummary.DisplayLastName,
                    SponsorMobile = sponsorSummary.PrimaryPhone,
                    SponsorEmail = sponsorSummary.EmailAddress,
                    JoinDate = AssociateInfo.SignupDate.ToUniversalTime(),
                    ActiveAutoship = _orderService.GetOrders(new [] { req.AssociateId }).Result.Where(o => o.OrderType == OrderType.Autoship).Any()
                };

                var jsonReq = JsonConvert.SerializeObject(ZiplingoEngagementRequest);
                CallZiplingoEngagementApi(jsonReq, "Contact/CreateContactV2");
            }
            catch (Exception e)
            {
                //_logger.LogError($"{ClassName}.UpdateContact", $"Exception occurred attempting to UpdateContact for associate {req.AssociateId}", e);
            }
        }


        public HttpResponseMessage CallZiplingoEngagementApi(string jsonData, string apiMethod)
        {
            try
            {
                var settings = _ZiplingoEngagementRepository.GetSettings();

                //httpClient call
                var apiUrl = settings.ApiUrl + apiMethod;
                HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("POST"), new Uri(apiUrl));
                request.Content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                var data = _httpClientService.PostRequestByUsername(request, settings.Username, settings.Password);

                return data;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void ResetSettings(CommandRequest commandRequest)
        {
            try
            {
                _ZiplingoEngagementRepository.ResetSettings();
            }
            catch (Exception ex)
            {
              //  _logger.LogError($"{ClassName}.ResetZiplingoSettings", $"Exception occurred attempting to ResetSettings", ex);
            }
        }

        public async void SendOrderShippedEmail(int packageId, string trackingNumber)
        {
            var orderModel = new OrderDetailModel();
            var shipInfo = _ZiplingoEngagementRepository.GetOrderNumber(packageId);
            orderModel.TrackingNumber = trackingNumber;
            orderModel.Carrier = shipInfo.Carrier;
            orderModel.ShipMethodId = shipInfo.ShipMethodId;
            orderModel.DateShipped = shipInfo.DateShipped;
            orderModel.Order = await _orderService.GetOrderByOrderNumber(shipInfo.OrderNumber);
            if (orderModel.Order.OrderType == OrderType.Autoship)
            {
                var autoShipInfo = _ZiplingoEngagementRepository.GetAutoshipFromOrder(shipInfo.OrderNumber);
                orderModel.AutoshipId = autoShipInfo.AutoshipId;
                CallOrderZiplingoEngagementTriggerForShipped(orderModel, "AutoOrderShipped");
            }
            if (orderModel.Order.OrderType == OrderType.Standard)
            {
                CallOrderZiplingoEngagementTriggerForShipped(orderModel, "OrderShipped");
            }
        }

        public void AssociateBirthDateTrigger()
        {
            var settings = _ZiplingoEngagementRepository.GetSettings();
            if (settings.AllowBirthday)
            {
                var associateInfo = _ZiplingoEngagementRepository.AssociateBirthdayWishesInfo();
                if (associateInfo == null) return;

                foreach (var assoInfo in associateInfo)
                {
                    AssociateInfo asso = new AssociateInfo();
                    asso.AssociateId = assoInfo.AssociateId;
                    asso.Birthdate = assoInfo.Birthdate;
                    asso.EmailAddress = assoInfo.EmailAddress;
                    asso.FirstName = assoInfo.FirstName;
                    asso.LastName = assoInfo.LastName;
                    CallOrderZiplingoEngagementTriggerForBirthDayWishes(asso, "AssociateBirthdayWishes");
                }
            }
        }

        public void AssociateWorkAnniversaryTrigger()
        {
            var settings = _ZiplingoEngagementRepository.GetSettings();
            if (settings.AllowAnniversary)
            {
                var associateInfo = _ZiplingoEngagementRepository.AssociateWorkAnniversaryInfo();
                if (associateInfo == null) return;

                foreach (var assoInfo in associateInfo)
                {
                    AssociateInfo asso = new AssociateInfo();
                    asso.AssociateId = assoInfo.AssociateId;
                    asso.SignupDate = assoInfo.SignupDate;
                    asso.EmailAddress = assoInfo.EmailAddress;
                    asso.FirstName = assoInfo.FirstName;
                    asso.LastName = assoInfo.LastName;
                    asso.TotalWorkingYears = assoInfo.TotalWorkingYears;
                    CallOrderZiplingoEngagementTriggerForWorkAnniversary(asso, "AssociateWorkAnniversary");
                }
            }
        }

        public EmailOnNotificationEvent OnNotificationEvent(NotificationEvent notification)
        {
            if((int)notification.EventType == 1)
            {
               return CallRankAdvancementEvent(notification.EventValue);
            }
            return null;
        }
        public LogRealtimeRankAdvanceHookResponse LogRealtimeRankAdvanceEvent(LogRealtimeRankAdvanceHookRequest req)
        {
            return LogRankAdvancement(req);
        }

        public  LogRealtimeRankAdvanceHookResponse LogRankAdvancement(LogRealtimeRankAdvanceHookRequest req)
        {
            try
            {
                var logMessage = String.Empty;
                AssociateRankAdvancement obj = new AssociateRankAdvancement();
                var settings = _ZiplingoEngagementRepository.GetSettings();
                var rankName = _rankService.GetRankName(req.NewRank);
                var associateInfo = _distributorService.GetAssociate(req.AssociateId);
                if (settings.AllowRankAdvancement)
                {
                    obj.Rank = req.NewRank;
                    obj.RankName = rankName.Result;
                    obj.AssociateId = req.AssociateId;
                    obj.FirstName = associateInfo.Result.DisplayFirstName;
                    obj.LastName = associateInfo.Result.DisplayLastName;
					_customLogRepository.SaveLog(obj.AssociateId, 0, "Debug log : Rank Advancement", "Payload", "", "", "", "", "");
					CallOrderZiplingoEngagementTriggerForAssociateRankAdvancement(obj, "RankAdvancement");
                }
                return null;
            }
            catch (Exception ex)
            {
              //  _logger.LogError(ex, "Exception occurred in Rank Advancement");
            }
            return null;
        }
        public  EmailOnNotificationEvent CallRankAdvancementEvent(NotificationEvent notification)
        {
            try
            {
                var settings = _ZiplingoEngagementRepository.GetSettings();
                if (settings.AllowRankAdvancement)
                {
                    AssociateRankAdvancement obj = new AssociateRankAdvancement();
                    string str = string.Empty;
                    var rank = 0;
                    var rankName = string.Empty;
                    try
                    {
                        if (!String.IsNullOrEmpty(Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(notification.EventValue)))
                        {
                            str = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(notification.EventValue);
                            RankObj objRank = JsonConvert.DeserializeObject<RankObj>(str);
                             rank = objRank.Rank;
                             rankName = _rankService.GetRankName(rank).Result;
                        }
                    }
                    catch (Exception ex) {
                        str = ex.Message;
                    }
                    var distribuWebExtensionnfo =  _distributorService.GetAssociate(notification.AssociateId);
                    obj.Rank = rank;
                    obj.RankName = rankName;
                    obj.AssociateId = notification.AssociateId;
                    obj.FirstName = distribuWebExtensionnfo.Result.DisplayFirstName;
                    obj.LastName = distribuWebExtensionnfo.Result.DisplayLastName;
                    CallOrderZiplingoEngagementTriggerForAssociateRankAdvancement(obj, "RankAdvancement");
                }
                return null;
            }
            catch (Exception ex)
            {
              //  _logger.LogError(ex, "Exception occurred in Rank Advancement", JsonConvert.SerializeObject(notification));
            }
            return null;
        }

        private class RankObj
        {
            public int Rank { get; set; }
        }


        public async void SevenDaysBeforeAutoshipTrigger()
        {
            var autoships = _ZiplingoEngagementRepository.SevenDaysBeforeAutoshipInfo();
            if (autoships == null) return;
            try
            {
                var company = await _companyService.GetCompany();
                var settings = _ZiplingoEngagementRepository.GetSettings();
                foreach (AutoshipInfo autoinfo in autoships)
                {
                    try
                    {
                        SevenDayAutoshipModel autoship = new SevenDayAutoshipModel();
                        autoship.AutoshipId = autoinfo.AutoshipId;
                        autoship.AssociateId = autoinfo.AssociateId;
                        autoship.UplineID = autoinfo.UplineID;
                        autoship.BackOfficeID = autoinfo.BackOfficeID;
                        autoship.NextProcessDate = autoinfo.NextProcessDate;
                        autoship.StartDate = autoinfo.StartDate;
                        autoship.FirstName = autoinfo.FirstName;
                        autoship.LastName = autoinfo.LastName;
                        autoship.EmailAddress = autoinfo.EmailAddress;
                        autoship.PrimaryPhone = autoinfo.PrimaryPhone;
                        autoship.SponsorName = autoinfo.SponsorName;
                        autoship.SponsorEmail = autoinfo.SponsorEmail;
                        autoship.SponsorMobile = autoinfo.SponsorMobile;
                        autoship.OrderNumber = autoinfo.OrderNumber;
                        autoship.CompanyDomain = company.BackOfficeHomePageURL;
                        autoship.LogoUrl = settings.LogoUrl;
                        autoship.CompanyName = settings.CompanyName;
                        if (autoship.OrderNumber > 0)
                        {

                        }
                        var strData = JsonConvert.SerializeObject(autoship);
                        ZiplingoEngagementRequest request = new ZiplingoEngagementRequest { associateid = autoship.AssociateId, companyname = settings.CompanyName, eventKey = "SevenDaysBeforeAutoship", data = strData };
                        var jsonReq = JsonConvert.SerializeObject(request);
                        CallZiplingoEngagementApi(jsonReq, "Campaign/ExecuteTrigger");
                    }
                    catch (Exception ex)
                    {
                      //  _logger.LogError(ex, "Exception occurred in Seven DayTrigger autoship record", JsonConvert.SerializeObject(autoinfo));
                    }
                }

            }
            catch (Exception e)
            {
               // _logger.LogError($"{ClassName}.Seven DayTrigger", $"Exception occurred attempting to Seven DayTrigger", e);
            }
        }

        public async void FiveDayRunTrigger(List<AutoshipInfo> autoships)
        {
            try
            {
                var company = await _companyService.GetCompany();
                var settings = _ZiplingoEngagementRepository.GetSettings();

                for (int i = 0; i < autoships.Count; i = i + 100)
                {
                    List<FivedayAutoshipModel> autoshipList = new List<FivedayAutoshipModel>();
                    var items = autoships.Skip(i).Take(100);
                    foreach (var autoship in items)
                    {
                        FivedayAutoshipModel autoObj = new FivedayAutoshipModel();
                        autoObj.AssociateId = autoship.AssociateId;
                        autoObj.AutoshipId = autoship.AutoshipId;
                        autoObj.UplineID = autoship.UplineID;
                        autoObj.BackOfficeID = autoship.BackOfficeID;
                        autoObj.FirstName = autoship.FirstName;
                        autoObj.LastName = autoship.LastName;
                        autoObj.PrimaryPhone = autoship.PrimaryPhone;
                        autoObj.StartDate = autoship.StartDate;
                        autoObj.NextProcessDate = autoship.NextProcessDate;
                        autoObj.SponsorName = autoship.SponsorName;
                        autoObj.SponsorEmail = autoship.SponsorEmail;
                        autoObj.SponsorMobile = autoship.SponsorMobile;
                        autoObj.OrderNumber = autoship.OrderNumber;
                        autoObj.CompanyDomain = company.BackOfficeHomePageURL;
                        autoObj.LogoUrl = settings.LogoUrl;
                        autoObj.CompanyName = settings.CompanyName;
                        autoshipList.Add(autoObj);
                    }
                    CallFiveDayRunTrigger(autoshipList);
                }
            }
            catch (Exception e)
            {
               // _logger.LogError($"5DayTrigger", $"Exception occurred attempting to 5DayTrigger", e);
            }
        }
        public async void CallFiveDayRunTrigger(List<FivedayAutoshipModel> autoshipList)
        {
            try
            {
                var settings = _ZiplingoEngagementRepository.GetSettings();
                var company = await _companyService.GetCompany();
                List<AssociateDetail> objautoshipListDetail = new List<AssociateDetail>();
                foreach (var autoship in autoshipList)
                {
                    AssociateDetail associateDetail = new AssociateDetail();
                    int enrollerID = 0;
                    int sponsorID = 0;
                    if (_treeService.GetNodeDetail(new NodeId(autoship.AssociateId, 0), TreeType.Enrollment).Result.UplineId != null)
                    {
                        enrollerID = _treeService.GetNodeDetail(new NodeId(autoship.AssociateId, 0), TreeType.Enrollment)?.Result.UplineId.AssociateId ?? 0;
                    }
                    if (_treeService.GetNodeDetail(new NodeId(autoship.AssociateId, 0), TreeType.Unilevel).Result.UplineId != null)
                    {
                        sponsorID = _treeService.GetNodeDetail(new NodeId(autoship.AssociateId, 0), TreeType.Unilevel)?.Result.UplineId.AssociateId ?? 0;
                    }

                    Associate sponsorSummary = new Associate();
                    Associate enrollerSummary = new Associate();
                    if (enrollerID <= 0)
                    {
                        enrollerSummary = new Associate();
                    }
                    else
                    {
                        enrollerSummary = await _distributorService.GetAssociate(enrollerID);
                    }
                    if (sponsorID > 0)
                    {
                        sponsorSummary = await _distributorService.GetAssociate(sponsorID);
                    }
                    else
                    {
                        sponsorSummary = enrollerSummary;
                    }
                    var associateSummary = await _distributorService.GetAssociate(autoship.AssociateId);
                    AssociateInfo data = new AssociateInfo
                    {
                        AssociateId = autoship.AssociateId,
                        EmailAddress = associateSummary.EmailAddress,
                        Birthdate = associateSummary.BirthDate.ToShortDateString(),
                        FirstName = associateSummary.LegalFirstName,
                        LastName = associateSummary.LegalLastName,
                        CompanyDomain = company.BackOfficeHomePageURL,
                        LogoUrl = settings.LogoUrl,
                        CompanyName = settings.CompanyName,
                        EnrollerId = enrollerSummary.AssociateId,
                        SponsorId = sponsorSummary.AssociateId,
                        CommissionActive = true,
                        FivedayAutoshipDetails = autoship,
                        EnrollerName = enrollerSummary.DisplayFirstName + ' ' + enrollerSummary.DisplayLastName,
                        EnrollerMobile = enrollerSummary.PrimaryPhone,
                        EnrollerEmail = enrollerSummary.EmailAddress,
                        SponsorName = sponsorSummary.DisplayLastName + ' ' + sponsorSummary.DisplayLastName,
                        SponsorMobile = sponsorSummary.PrimaryPhone,
                        SponsorEmail = sponsorSummary.EmailAddress
                    };
                    associateDetail.associateId = autoship.AssociateId;
                    associateDetail.data = JsonConvert.SerializeObject(data);
                    objautoshipListDetail.Add(associateDetail);
                }

                var strData = objautoshipListDetail;
                ZiplingoEngagementListRequest request = new ZiplingoEngagementListRequest { companyname = settings.CompanyName, eventKey = "FiveDayAutoship", dataList = strData };
                var jsonReq = JsonConvert.SerializeObject(request);
                CallZiplingoEngagementApi(jsonReq, "Campaign/ExecuteTriggersList");
            }

            catch (Exception e)
            {
               // _logger.LogError($"5DayTrigger", $"Exception occurred attempting to 5DayTrigger", e);
            }
        }


        public async void ExpirationCardTrigger(List<CardInfo> cardinfo)
        {
            try
            {
                var company = await _companyService.GetCompany();
                var settings = _ZiplingoEngagementRepository.GetSettings();

                foreach (CardInfo info in cardinfo)
                {
                    try
                    {
                        AssociateCardInfoModel assoObj = new AssociateCardInfoModel();
                        assoObj.FirstName = info.FirstName;
                        assoObj.LastName = info.LastName;
                        assoObj.PrimaryPhone = info.PrimaryPhone;
                        assoObj.Email = info.PrimaryPhone;
                        assoObj.CardDate = info.ExpirationDate;
                        assoObj.CardLast4Degit = info.Last4DegitOfCard;
                        assoObj.CompanyDomain = company.BackOfficeHomePageURL;
                        assoObj.LogoUrl = settings.LogoUrl;
                        assoObj.CompanyName = settings.CompanyName;

                        var strData = JsonConvert.SerializeObject(assoObj);
                        ZiplingoEngagementRequest request = new ZiplingoEngagementRequest { associateid = info.AssociateId, companyname = settings.CompanyName, eventKey = "UpcomingExpiryCard", data = strData };
                        var jsonReq = JsonConvert.SerializeObject(request);
                        CallZiplingoEngagementApi(jsonReq, "Campaign/ExecuteTrigger");
                    }
                    catch (Exception ex)
                    {
                     //   _logger.LogError(ex, "Exception occurred in Get Card Expiring Info Before 30 Days", JsonConvert.SerializeObject(info));
                    }
                }

            }
            catch (Exception e)
            {
               // _logger.LogError($"{ClassName}.5DayTrigger", $"Exception occurred attempting to 5DayTrigger", e);
            }
        }

        public async void UpdateAssociateType(int associateId, string oldAssociateType, string newAssociateType, int newAssociateTypeId)
        {
            try
            {
                var company = await _companyService.GetCompany();
                var associateTypeModel = new AssociateTypeModel();
                var settings = _ZiplingoEngagementRepository.GetSettings();
                var associateSummary = await _distributorService.GetAssociate(associateId);
                var associateOrders = _orderService.GetOrdersByAssociateId(associateId, "");
                var UserName = _ZiplingoEngagementRepository.GetUsernameById(Convert.ToString(associateId));
                int enrollerID = 0;
                int sponsorID = 0;
                if (_treeService.GetNodeDetail(new NodeId(associateId, 0), TreeType.Enrollment)?.Result.UplineId != null)
                {
                    enrollerID = _treeService.GetNodeDetail(new NodeId(associateId, 0), TreeType.Enrollment)?.Result.UplineId.AssociateId ?? 0;
                }
                if (_treeService.GetNodeDetail(new NodeId(associateId, 0), TreeType.Unilevel).Result.UplineId != null)
                {
                    sponsorID = _treeService.GetNodeDetail(new NodeId(associateId, 0), TreeType.Unilevel)?.Result.UplineId.AssociateId ?? 0;
                }

                Associate sponsorSummary = new Associate();
                Associate enrollerSummary = new Associate();
                if (enrollerID <= 0)
                {
                    enrollerSummary = new Associate();
                }
                else
                {
                    enrollerSummary = await _distributorService.GetAssociate(enrollerID);
                }
                if (sponsorID > 0)
                {
                    sponsorSummary = await  _distributorService.GetAssociate(sponsorID);
                }
                else
                {
                    sponsorSummary = enrollerSummary;
                }
                associateTypeModel.AssociateId = associateId;
                associateTypeModel.FirstName = associateSummary.DisplayFirstName;
                associateTypeModel.LastName = associateSummary.DisplayLastName;
                associateTypeModel.Email = associateSummary.EmailAddress;
                associateTypeModel.Phone = (associateSummary.TextNumber == "" || associateSummary.TextNumber == null)
                    ? associateSummary.PrimaryPhone
                    : associateSummary.TextNumber;
                associateTypeModel.OldAssociateBaseType = oldAssociateType;
                associateTypeModel.NewAssociateBaseType = newAssociateType;
                associateTypeModel.CompanyDomain = company.BackOfficeHomePageURL;
                associateTypeModel.LogoUrl = settings.LogoUrl;
                associateTypeModel.CompanyName = settings.CompanyName;
                associateTypeModel.AssociateType = associateSummary.AssociateType;
                associateTypeModel.BackOfficeId = associateSummary.BackOfficeId;
                associateTypeModel.address = associateSummary.Address.AddressLine1 + " " + associateSummary.Address.AddressLine2 + " " + associateSummary.Address.AddressLine3;
                associateTypeModel.city = associateSummary.Address.City;
                associateTypeModel.birthday = associateSummary.BirthDate;
                associateTypeModel.CountryCode = associateSummary.Address.CountryCode;
                associateTypeModel.distributerId = associateSummary.BackOfficeId;
                associateTypeModel.region = associateSummary.Address.CountryCode;
                associateTypeModel.state = associateSummary.Address.State;
                associateTypeModel.zip = associateSummary.Address.PostalCode;
                associateTypeModel.UserName = UserName;
                associateTypeModel.WebAlias = UserName;
                associateTypeModel.CompanyUrl = company.BackOfficeHomePageURL;
                associateTypeModel.LanguageCode = associateSummary.LanguageCode;
                associateTypeModel.CommissionActive = true;
                associateTypeModel.EnrollerId = enrollerSummary.AssociateId;
                associateTypeModel.SponsorId = sponsorSummary.AssociateId;
                associateTypeModel.EnrollerName = enrollerSummary.DisplayFirstName + ' ' + enrollerSummary.DisplayLastName;
                associateTypeModel.EnrollerMobile = enrollerSummary.PrimaryPhone;
                associateTypeModel.EnrollerEmail = enrollerSummary.EmailAddress;
                associateTypeModel.SponsorName = sponsorSummary.DisplayFirstName + ' ' + sponsorSummary.DisplayLastName;
                associateTypeModel.SponsorMobile = sponsorSummary.PrimaryPhone;
                associateTypeModel.SponsorEmail = sponsorSummary.EmailAddress;
                associateTypeModel.JoinDate = associateSummary.SignupDate.ToUniversalTime();
                associateTypeModel.AssociateStatus = associateSummary.StatusId;
                associateTypeModel.ActiveAutoship = associateOrders.Result.Where(o => o.OrderType == OrderType.Autoship).Any();
                //associateTypeModel.OrderDate = order.OrderDate, // OrderDate added for first order

                var strData = JsonConvert.SerializeObject(associateTypeModel);

                AssociateTypeChange request = new AssociateTypeChange
                {
                    associateTypeId = newAssociateTypeId,
                    associateid = associateId,
                    companyname = settings.CompanyName,
                    eventKey = "AssociateTypeChange",
                    data = strData
                };
                var jsonReq = JsonConvert.SerializeObject(request);
                CallZiplingoEngagementApi(jsonReq, "Campaign/ChangeAssociateType");

            }
            catch (Exception e)
            {
              //  _logger.LogError("ZiplingoEngagementService.UpdateAssociateType", $"Error trying to send emails to ZipLingo {e.Message} On Type Change", e);
            }
        }

        //Upcoming Service Expiry Trigger
        public void UpcomingServiceExpiry()
        {
            try
            {
                var settings = _ZiplingoEngagementRepository.GetSettings();

                var services = _ZiplingoEngagementRepository.UpcomingServiceExpiry();

                foreach (var service in services)
                {
                    var jsonZiplingoEngagementRequest = JsonConvert.SerializeObject(service);

                    ZiplingoEngagementRequest request = new ZiplingoEngagementRequest();

                    request = new ZiplingoEngagementRequest { associateid = service.associateid, companyname = settings.CompanyName, eventKey = "UpcomingServiceExpiry", data = jsonZiplingoEngagementRequest };

                    var jsonReq = JsonConvert.SerializeObject(request);

                    CallZiplingoEngagementApi(jsonReq, "Campaign/ExecuteTrigger");
                }
            }
            catch (Exception e)
            {
              //  _logger.LogError(e, "Exception occurred in UpcomingServiceExpiry ", JsonConvert.SerializeObject(e.Message));
            }
        }

        //Autoship TRIGGER
        public async void CreateAutoshipTrigger(Autoship autoshipInfo)
        {
            try
            {
                var company = _companyService.GetCompany();
                var settings = _ZiplingoEngagementRepository.GetSettings();
				var associateInfo = await _distributorService.GetAssociate(autoshipInfo.AssociateId);
				AutoshipInfoMap req = new AutoshipInfoMap();

                req.AssociateId = autoshipInfo.AssociateId;
                req.AutoshipId = autoshipInfo.AutoshipId;
                req.AutoshipType = autoshipInfo.AutoshipType.ToString();
                req.CurrencyCode = autoshipInfo.CurrencyCode;
                req.Custom = autoshipInfo.Custom;
                req.Frequency = autoshipInfo.Frequency.ToString();
                req.FrequencyString = autoshipInfo.FrequencyString;
                req.LastChargeAmount = autoshipInfo.LastChargeAmount;
                req.LastProcessDate = autoshipInfo.LastProcessDate;
                req.LineItems = autoshipInfo.LineItems;
                req.NextProcessDate = autoshipInfo.NextProcessDate;
                req.PaymentMerchantId = autoshipInfo.PaymentMerchantId;
                req.PaymentMethodId = autoshipInfo.PaymentMethodId;
				req.FirstName = associateInfo.DisplayFirstName;
				req.LastName = associateInfo.DisplayLastName;
				req.Email = associateInfo.EmailAddress;
				req.Phone = associateInfo.PrimaryPhone;
                req.ShipAddress = autoshipInfo.ShipAddress;
				req.ProductNames = String.Join(",", autoshipInfo.LineItems.Select(l => l.ProductName));
				req.ShipMethodId = autoshipInfo.ShipMethodId;
                req.StartDate = autoshipInfo.StartDate;
                req.Status = autoshipInfo.Status;
                req.SubTotal = autoshipInfo.SubTotal;
                req.TotalCV = autoshipInfo.TotalCV;
                req.TotalQV = autoshipInfo.TotalQV;

                var strData = JsonConvert.SerializeObject(req);

                ZiplingoEngagementRequest request = new ZiplingoEngagementRequest { associateid = autoshipInfo.AssociateId, companyname = settings.CompanyName, eventKey = "CreateAutoship", data = strData };
                var jsonReq = JsonConvert.SerializeObject(request);
                CallZiplingoEngagementApi(jsonReq, "Campaign/ExecuteTrigger");
            }
            catch (Exception e)
            {
             //   _logger.LogError($"{ClassName}.CreateAutoshipTrigger", $"Exception occured in attempting CreateAutoshipTrigger", e);
            }
        }

        public async void ExecuteCommissionEarned()
        {
            try
            {
                var settings = _ZiplingoEngagementRepository.GetSettings();

                var paymentData = await _paymentProcessingService.FindPaidPayments(DateTime.Now.Date.AddDays(-1), DateTime.Now.Date, "");

                for (int i = 0; i < paymentData.Length; i = i + 100)
                {
                    List<CommissionPayment> paymentDataList = new List<CommissionPayment>();
                    var items = paymentData.Skip(i).Take(100);
                    foreach (var data in items)
                    {
                        CommissionPayment pyModel = new CommissionPayment();
                        pyModel.BatchId = data.BatchId;
                        pyModel.Details = data.Details;
                        pyModel.MerchantId = data.MerchantId;
                        pyModel.PaymentUniqueId = data.PaymentUniqueId;
                        pyModel.CountryCode = data.CountryCode;
                        pyModel.TaxId = data.TaxId;
                        pyModel.Amount = data.Amount;
                        pyModel.Fees = data.Fees;
                        pyModel.Holdings = data.Holdings;
                        pyModel.Total = data.Total;
                        pyModel.ExchangeRate = data.ExchangeRate;
                        pyModel.ExchangeCurrencyCode = data.ExchangeCurrencyCode;
                        pyModel.PaymentStatus = data.PaymentStatus;
                        pyModel.DatePaid = data.DatePaid;
                        pyModel.TransactionNumber = data.TransactionNumber;
                        pyModel.CheckNumber = data.CheckNumber;
                        pyModel.ErrorMessage = data.ErrorMessage;
                        pyModel.MerchantCustomFields = data.MerchantCustomFields;
                        pyModel.AssociateId = data.AssociateId;
                        paymentDataList.Add(pyModel);
                    }
                    CallExecuteCommissionEarnedTrigger(paymentDataList);
                }

            }
            catch (Exception e)
            {
             //   _logger.LogError(e, "Exception occurred in ExecuteCommissionEarned ", JsonConvert.SerializeObject(e.Message));
            }
        }

        public async void UpdateAutoshipTrigger(DirectScale.Disco.Extension.Autoship updatedAutoshipInfo)
        {
            try
            {
                var company = _companyService.GetCompany();
                var settings = _ZiplingoEngagementRepository.GetSettings();
				var associateInfo = await _distributorService.GetAssociate(updatedAutoshipInfo.AssociateId);
				AutoshipInfoMap req = new AutoshipInfoMap();

                req.AssociateId = updatedAutoshipInfo.AssociateId;
                req.AutoshipId = updatedAutoshipInfo.AutoshipId;
                req.AutoshipType = updatedAutoshipInfo.AutoshipType.ToString();
                req.CurrencyCode = updatedAutoshipInfo.CurrencyCode;
                req.Custom = updatedAutoshipInfo.Custom;
                req.Frequency = updatedAutoshipInfo.Frequency.ToString();
                req.FrequencyString = updatedAutoshipInfo.FrequencyString;
                req.LastChargeAmount = updatedAutoshipInfo.LastChargeAmount;
                req.LastProcessDate = updatedAutoshipInfo.LastProcessDate;
                //req.LineItems = string.Join(",", updatedAutoshipInfo.LineItems.Select(x => x.ProductName).ToArray());
                req.LineItems = updatedAutoshipInfo.LineItems;
                req.NextProcessDate = updatedAutoshipInfo.NextProcessDate;
                req.PaymentMerchantId = updatedAutoshipInfo.PaymentMerchantId;
                req.PaymentMethodId = updatedAutoshipInfo.PaymentMethodId;
                req.ShipAddress = updatedAutoshipInfo.ShipAddress;
				req.FirstName = associateInfo.DisplayFirstName;
				req.LastName = associateInfo.DisplayLastName;
				req.Email = associateInfo.EmailAddress;
				req.Phone = associateInfo.PrimaryPhone;
				req.ProductNames = String.Join(",", updatedAutoshipInfo.LineItems.Select(l => l.ProductName));
				req.ShipMethodId = updatedAutoshipInfo.ShipMethodId;
                req.StartDate = updatedAutoshipInfo.StartDate;
                req.Status = updatedAutoshipInfo.Status;
                req.SubTotal = updatedAutoshipInfo.SubTotal;
                req.TotalCV = updatedAutoshipInfo.TotalCV;
                req.TotalQV = updatedAutoshipInfo.TotalQV;

                var strData = JsonConvert.SerializeObject(req);
                ZiplingoEngagementRequest request = new ZiplingoEngagementRequest { associateid = updatedAutoshipInfo.AssociateId, companyname = settings.CompanyName, eventKey = "AutoshipChanged", data = strData };
                var jsonReq = JsonConvert.SerializeObject(request);
                CallZiplingoEngagementApi(jsonReq, "Campaign/ExecuteTrigger");
            }
            catch (Exception ex)
            {
              //  _logger.LogError($"{ClassName}.AutoshipChangedTrigger", $"Exception occured in attempting AutoshipChangedTrigger", ex);
            }
        }


        public async void CallExecuteCommissionEarnedTrigger(List<CommissionPayment> payments)
        {
            try
            {
                var settings = _ZiplingoEngagementRepository.GetSettings();
                var company = await _companyService.GetCompany();

                List<AssociateDetail> objpayListDetail = new List<AssociateDetail>();
                foreach (var payment in payments)
                {
                    AssociateDetail associateDetail = new AssociateDetail();
                    int enrollerID = 0;
                    int sponsorID = 0;
                    if (_treeService.GetNodeDetail(new NodeId(payment.AssociateId, 0), TreeType.Enrollment).Result.UplineId != null)
                    {
                        enrollerID = _treeService.GetNodeDetail(new NodeId(payment.AssociateId, 0), TreeType.Enrollment)?.Result.UplineId.AssociateId ?? 0;
                    }
                    if (_treeService.GetNodeDetail(new NodeId(payment.AssociateId, 0), TreeType.Unilevel).Result.UplineId != null)
                    {
                        sponsorID = _treeService.GetNodeDetail(new NodeId(payment.AssociateId, 0), TreeType.Unilevel)?.Result.UplineId.AssociateId ?? 0;
                    }

                    Associate sponsorSummary = new Associate();
                    Associate enrollerSummary = new Associate();
                    if (enrollerID <= 0)
                    {
                        enrollerSummary = new Associate();
                    }
                    else
                    {
                        enrollerSummary = await _distributorService.GetAssociate(enrollerID);
                    }
                    if (sponsorID > 0)
                    {
                        sponsorSummary = await _distributorService.GetAssociate(sponsorID);
                    }
                    else
                    {
                        sponsorSummary = enrollerSummary;
                    }
                    var associateSummary = await _distributorService.GetAssociate(payment.AssociateId);
                    AssociateInfoCommissionEarned data = new AssociateInfoCommissionEarned
                    {
                        AssociateId = payment.AssociateId,
                        EmailAddress = associateSummary.EmailAddress,
                        Birthdate = associateSummary.BirthDate.ToShortDateString(),
                        FirstName = associateSummary.LegalFirstName,
                        LastName = associateSummary.LegalLastName,
                        CompanyDomain = company.BackOfficeHomePageURL,
                        LogoUrl = settings.LogoUrl,
                        CompanyName = settings.CompanyName,
                        EnrollerId = enrollerSummary.AssociateId,
                        SponsorId = sponsorSummary.AssociateId,
                        CommissionActive = true,
                        MerchantCustomFields = payment.MerchantCustomFields,
                        CommissionDetails = MapCommissionPayment(payment),
                        CommissionPaymentDetails = payment.Details,
                        EnrollerName = enrollerSummary.DisplayFirstName + ' ' + enrollerSummary.DisplayLastName,
                        EnrollerMobile = enrollerSummary.PrimaryPhone,
                        EnrollerEmail = enrollerSummary.EmailAddress,
                        SponsorName = sponsorSummary.DisplayFirstName + ' ' + sponsorSummary.DisplayFirstName,
                        SponsorMobile = sponsorSummary.PrimaryPhone,
                        SponsorEmail = sponsorSummary.EmailAddress
                    };
                    associateDetail.associateId = payment.AssociateId;
                    associateDetail.data = JsonConvert.SerializeObject(data);
                    objpayListDetail.Add(associateDetail);
                }

                var strData = objpayListDetail;
                ZiplingoEngagementListRequest request = new ZiplingoEngagementListRequest { companyname = settings.CompanyName, eventKey = "CommissionEarned", dataList = strData };
                var jsonReq = JsonConvert.SerializeObject(request);
                CallZiplingoEngagementApi(jsonReq, "Campaign/ExecuteTriggersList");
            }
            catch (Exception e)
            {
              //  _logger.LogError(e, "Exception occurred in ExecuteCommissionEarned ", JsonConvert.SerializeObject(e.Message));
            }
        }

        public CommissionPaymentModel MapCommissionPayment(CommissionPayment commission)
        {
            if (commission != null)
            {
                return new CommissionPaymentModel
                {
                    Id = commission.Id,
                    Amount = commission.Amount,
                    AssociateId = commission.AssociateId,
                    BatchId = commission.BatchId,
                    CheckNumber = commission.CheckNumber,
                    CountryCode = commission.CountryCode,
                    DatePaid = commission.DatePaid,
                    ErrorMessage = commission.ErrorMessage,
                    ExchangeCurrencyCode = commission.ExchangeCurrencyCode,
                    ExchangeRate = commission.ExchangeRate,
                    Fees = commission.Fees,
                    Holdings = commission.Holdings,
                    MerchantId = commission.MerchantId,
                    PaymentStatus = commission.PaymentStatus,
                    PaymentUniqueId = commission.PaymentUniqueId,
                    TaxId = commission.TaxId,
                    Total = commission.Total,
                    TransactionNumber = commission.TransactionNumber
                };
            }

            return null;
        }
        public void AssociateStatusSync(List<GetAssociateStatusModel> associateStatuses)
        {
            try
            {
                if (associateStatuses.Count != 0)
                {
                    foreach (var item in associateStatuses)
                    {
                        var associateSummary = _distributorService.GetAssociate(item.AssociateID).Result;
                        associateSummary.StatusId = item.CurrentStatusId;
                        UpdateContact(associateSummary);
                    }
                }

            }
            catch (Exception ex)
            {
                
            }

        }


    }
}
