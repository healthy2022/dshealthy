using System;
using WebExtension.ThirdParty.Interfaces;
using WebExtension.ThirdParty.Model;
using WebExtension.ThirdParty.ZiplingoEngagement.Model;
using System.Collections.Generic;
using Dapper;
using DirectScale.Disco.Extension.Services;
using System.Linq;
using DirectScale.Disco.Extension;

namespace WebExtension.ThirdParty
{
    public class ZiplingoEngagementRepository : IZiplingoEngagementRepository
    {
        private readonly IDataService _dataService;
        private readonly ISettingsService _settingsService;

        public ZiplingoEngagementRepository(
            IDataService dataService,
            ISettingsService settingsService
            )
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        

        public ZiplingoEngagementSettings GetSettings()
        {
            EnvironmentType env = _settingsService.ExtensionContext().Result.EnvironmentType;
            using (var dbConnection = new System.Data.SqlClient.SqlConnection(_dataService.GetClientConnectionString().Result))
            {
                var parameters = new
                {
                    Environment = (env == EnvironmentType.Live ? "Live" : "Stage")
                };
                var settingsQuery = "SELECT * FROM Client.ZiplingoEngagementSettings where Environment = @Environment";

                return dbConnection.QueryFirstOrDefault<ZiplingoEngagementSettings>(settingsQuery, parameters);
            }
        }

        public void UpdateSettings(ZiplingoEngagementSettingsRequest settings)
        {
            EnvironmentType env = _settingsService.ExtensionContext().Result.EnvironmentType;
            using (var dbConnection = new System.Data.SqlClient.SqlConnection(_dataService.GetClientConnectionString().Result))
            {
                var parameters = new
                {
                    settings.ApiUrl,
                    settings.Username,
                    settings.Password,
                    settings.LogoUrl,
                    settings.CompanyName,
                    settings.AllowBirthday,
                    settings.AllowAnniversary,
                    settings.AllowRankAdvancement,
                    Environment = (env == EnvironmentType.Live ? "Live" : "Stage")
                };

                var updateStatement = @"UPDATE Client.ZiplingoEngagementSettings SET ApiUrl = @ApiUrl,  Username = @Username, Password = @Password, LogoUrl = @LogoUrl, CompanyName = @CompanyName, AllowBirthday = @AllowBirthday, AllowAnniversary = @AllowAnniversary, AllowRankAdvancement = @AllowRankAdvancement where Environment = @Environment";
                dbConnection.Execute(updateStatement, parameters);
            }
            
        }

        public void ResetSettings()
        {
            try
            {
                using (var dbConnection = new System.Data.SqlClient.SqlConnection(_dataService.GetClientConnectionString().Result))
                {
                    var settings = GetSettings();
                    var parameters = new
                    {
                        Username = "HealthyHomeAPI",
                        Password = "d194855f-630b-4f72-a9e2-98bb5c2c4ffc",
                        ApiUrl = "http://unifiedbetaapi.ziplingo.com/api/",
                        LogoUrl = "https://az708237.vo.msecnd.net/WebExtension/images/376843ac-31be-42e7-9f7e-a072056b572e",
                        settings.CompanyName
                    };

                    var query = @"MERGE INTO Client.ZiplingoEngagementSettings WITH (HOLDLOCK) AS TARGET 
                USING 
                    (SELECT @Username AS 'Username', @Password AS 'Password', @ApiUrl AS 'ApiUrl', @LogoUrl AS 'LogoUrl', @CompanyName AS 'CompanyName'
                ) AS SOURCE 
                    ON SOURCE.CompanyName = TARGET.CompanyName
                WHEN MATCHED THEN 
                    UPDATE SET TARGET.Username = SOURCE.Username, TARGET.Password = SOURCE.Password, TARGET.ApiUrl = SOURCE.ApiUrl, TARGET.LogoUrl = SOURCE.LogoUrl
                WHEN NOT MATCHED BY TARGET THEN 
                    INSERT (Username, [Password], ApiUrl, LogoUrl,CompanyName) 
					VALUES (SOURCE.Username, SOURCE.Password, SOURCE.ApiUrl, SOURCE.LogoUrl, SOURCE.CompanyName);";

                    dbConnection.Execute(query, parameters);
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public ShipInfo GetOrderNumber(int packageId)
        {
            using (var dbConnection = new System.Data.SqlClient.SqlConnection(_dataService.GetClientConnectionString().Result))
            {
                ShipInfo info = new ShipInfo();
                var query = $"SELECT o.recordnumber as OrderNumber,p.ShipMethodId,p.Carrier,p.DateShipped FROM ORD_OrderPackages p JOIN ORD_Order o ON p.OrderNumber = o.recordnumber WHERE p.recordnumber ='{packageId}'";
                using (var reader = dbConnection.ExecuteReader(query))
                {
                    if (reader.Read())
                    {
                        info.OrderNumber = (Int32)reader["OrderNumber"];
                        info.ShipMethodId = (Int32)reader["ShipMethodId"];
                        info.Carrier = Convert.ToString(reader["Carrier"]);
                        info.DateShipped = Convert.ToString(reader["DateShipped"]);
                    }
                }
                return info;
            }
        }
        public List<AssociateInfo> AssociateBirthdayWishesInfo()
        {
            using (var dbConnection = new System.Data.SqlClient.SqlConnection(_dataService.GetClientConnectionString().Result))
            {
                List<AssociateInfo> objAsso = new List<AssociateInfo>();
                AssociateInfo info = new AssociateInfo();
                var query = $"SELECT Birthdate, recordnumber,EmailAddress,FirstName,LastName FROM crm_distributors WHERE datepart(dd, Birthdate) = datepart(dd, GETDATE()) AND datepart(mm, Birthdate) = datepart(mm, GETDATE()) AND Void = 0 AND StatusId<>5";

                using (var reader = dbConnection.ExecuteReader(query))
                {
                    while (reader.Read())
                    {
                        info.AssociateId = (Int32)reader["recordnumber"];
                        info.FirstName = Convert.ToString(reader["FirstName"]);
                        info.LastName = Convert.ToString(reader["LastName"]);
                        info.Birthdate = Convert.ToString(reader["Birthdate"]);
                        info.EmailAddress = Convert.ToString(reader["EmailAddress"]);
                        objAsso.Add(info);
                    }
                }
                return objAsso;
            }
        }

        public List<AssociateInfo> AssociateWorkAnniversaryInfo()
        {
            using (var dbConnection = new System.Data.SqlClient.SqlConnection(_dataService.GetClientConnectionString().Result))
            {
                List<AssociateInfo> objAsso = new List<AssociateInfo>();
                AssociateInfo info = new AssociateInfo();
                var query = $"SELECT SignupDate, recordnumber,EmailAddress,FirstName,LastName,DATEDIFF (yy, SignupDate, GETDATE()) AS 'TotalWorkingYears' FROM crm_distributors WHERE datepart(dd, SignupDate) = datepart(dd, GETDATE()) AND datepart(mm, SignupDate) = datepart(mm, GETDATE()) AND Void = 0 AND StatusId<>5";
                using (var reader = dbConnection.ExecuteReader(query))
                {
                    while (reader.Read())
                    {
                        info.AssociateId = (Int32)reader["recordnumber"];
                        info.FirstName = Convert.ToString(reader["FirstName"]);
                        info.LastName = Convert.ToString(reader["LastName"]);
                        info.SignupDate = Convert.ToString(reader["SignupDate"]);
                        info.TotalWorkingYears = (Int32)reader["TotalWorkingYears"];
                        info.EmailAddress = Convert.ToString(reader["EmailAddress"]);
                        objAsso.Add(info);
                    }
                }
                return objAsso;
            }
        }

        public List<AutoshipInfo> SevenDaysBeforeAutoshipInfo()
        {
            using (var dbConnection = new System.Data.SqlClient.SqlConnection(_dataService.GetClientConnectionString().Result))
            {
                List<AutoshipInfo> objAsso = new List<AutoshipInfo>();
                AutoshipInfo info = new AutoshipInfo();
                var query = @"SELECT 
                            [CRM_AutoShip].recordnumber as AutoshipID,
                            [CRM_AutoShip].AssociateID,
                            [CRM_AutoShip].NextProcessDate,
                            [CRM_AutoShip].StartDate,
                            [CRM_Distributors].FirstName,
                            [CRM_Distributors].LastName,
                            [CRM_Distributors].EmailAddress,
                            [CRM_Distributors].PrimaryPhone,
                            [CRM_Distributors].BackofficeID,
                            [CRM_EnrollTree].UplineID,
                            ED.FirstName + ' ' + ED.LastName as SponsorName,ED.EmailAddress as SponsorEmail,ED.PrimaryPhone as SponsorMobile,
                            (SELECT TOP 1 OrderId FROM CRM_AutoShipLog WHERE CRM_AutoShipLog.AutoshipId =[CRM_AutoShip].recordnumber Order BY last_modified desc) as OrderNumber,
                            DATEDIFF(DAY, GetDate(), NextProcessDate) AS 'Autoship Start In'
                            FROM[CRM_AutoShip]
                            INNER JOIN[dbo].[CRM_Distributors] ON[CRM_AutoShip].[AssociateID] = [CRM_Distributors].[recordnumber]
                            INNER JOIN[CRM_EnrollTree] ON CRM_EnrollTree.DistributorID =[CRM_AutoShip].AssociateID
                            INNER JOIN crm_distributors ED ON ED.recordnumber =[CRM_EnrollTree].UplineID
                            WHERE datepart(dd, NextProcessDate -7) = datepart(dd, GETDATE())
                            AND datepart(mm, NextProcessDate) = datepart(mm, GETDATE())
                            AND datepart(yy, NextProcessDate) = datepart(yy, GETDATE())";
                using (var reader = dbConnection.ExecuteReader(query))
                {
                    while (reader.Read())
                    {
                        info.AutoshipId = (Int32)reader["AutoshipID"];
                        info.AssociateId = (Int32)reader["AssociateID"];
                        info.UplineID = (Int32)reader["UplineID"];
                        info.BackOfficeID = Convert.ToString(reader["BackOfficeID"]);
                        info.FirstName = Convert.ToString(reader["FirstName"]);
                        info.LastName = Convert.ToString(reader["LastName"]);
                        info.NextProcessDate = (DateTime)(reader["NextProcessDate"]);
                        info.StartDate = (DateTime)reader["StartDate"];
                        info.EmailAddress = Convert.ToString(reader["EmailAddress"]);
                        info.PrimaryPhone = Convert.ToString(reader["PrimaryPhone"]);
                        info.SponsorName = Convert.ToString(reader["SponsorName"]);
                        info.SponsorEmail = Convert.ToString(reader["SponsorEmail"]);
                        info.SponsorMobile = Convert.ToString(reader["SponsorMobile"]);
                        info.OrderNumber = (Int32)reader["OrderNumber"];
                        objAsso.Add(info);
                    }
                }
                return objAsso;
            }
        }

        public AutoshipFromOrderInfo GetAutoshipFromOrder(int orderNumber)
        {
            using (var dbConnection = new System.Data.SqlClient.SqlConnection(_dataService.GetClientConnectionString().Result))
            {
                var qry = @"SELECT al.AutoShipId, a.LastProcessDate, a.NextProcessDate, a.Frequency
                FROM CRM_AutoShipLog al
                JOIN CRM_AutoShip a
                    ON al.AutoShipId = a.recordnumber
                WHERE al.OrderId = @orderNumber";

                return dbConnection.QueryFirstOrDefault<AutoshipFromOrderInfo>(qry, new { orderNumber });
            }
        }

        public string GetUsernameById(string associateId)
        {
            using (var dbConnection = new System.Data.SqlClient.SqlConnection(_dataService.GetClientConnectionString().Result))
            {
                var parameters = new
                {
                    BackOfficeId = new Dapper.DbString { IsAnsi = true, Length = 25, Value = associateId }
                };
                var query = @"SELECT Username FROM Users WHERE BackOfficeId = @BackofficeId";

                return dbConnection.QueryFirstOrDefault<string>(query, parameters);
            }
        }
        public string GetLastFoutDegitByOrderNumber(int orderId)
        {
            using (var dbConnection = new System.Data.SqlClient.SqlConnection(_dataService.GetClientConnectionString().Result))
            {
                var parameter = new { orderId };
                var query = @"SELECT Number FROM ORD_PaymentGatewayTransactions WHERE OrderNumber = @orderId";

                var result = dbConnection.QueryFirstOrDefault<string>(query, parameter);
                return result;
            }
        }
        public string GetStatusById(int statusId)
        {
            using (var dbConnection = new System.Data.SqlClient.SqlConnection(_dataService.GetClientConnectionString().Result))
            {
                var parameter = new { statusId };
                var query = @"SELECT StatusName FROM CRM_AssociateStatuses WHERE recordnumber = @statusId";

                var result = dbConnection.QueryFirstOrDefault<string>(query, parameter);
                return result;
            }
        }

        //Upcoming Service Expiry Trigger
        public List<UpcomingServiceExpiryModel> UpcomingServiceExpiry()
        {
            using (var dbConnection = new System.Data.SqlClient.SqlConnection(_dataService.GetClientConnectionString().Result))
            {
                //                var query = @"select distinct d.recordnumber as AssociateId,s.recordnumber as ServiceID, s.ServiceName,d.FirstName,d.LastName,acs.ExpirationDate  from [dbo].[CRM_AssociateServices] acs join [dbo].[CRM_Services] s on acs.ServiceId = s.recordnumber
                //join [dbo].[CRM_ServiceItems] si on s.recordnumber = si.ServiceID
                //join [dbo].[CRM_Distributors] d on acs.AssociateID = d.recordnumber
                //  where si.YearlyRenewal = 1 and acs.Void = 0 and d.StatusId=1 and 
                //  (DATEADD(dd, 0, DATEDIFF(dd, 0, acs.ExpirationDate)) = DATEADD(week,2,DATEADD(dd, 0, DATEDIFF(dd, 0, GETDATE()))) 
                //  OR DATEADD(dd, 0, DATEDIFF(dd, 0, acs.ExpirationDate)) = DATEADD(week,4,DATEADD(dd, 0, DATEDIFF(dd, 0, GETDATE()))) )
                //  order by expirationdate desc";

                var query = @"select DISTINCT d.recordnumber as AssociateId,s.recordnumber as ServiceID, s.ServiceName,d.FirstName,d.LastName,acs.ExpirationDate, 
DATEDIFF(DAY, GETDATE(), acs.ExpirationDate) as RemainingDays
 from [dbo].[CRM_AssociateServices] acs join [dbo].[CRM_Services] s on acs.ServiceId = s.recordnumber
join [dbo].[CRM_ServiceItems] si on s.recordnumber = si.ServiceID
join [dbo].[CRM_Distributors] d on acs.AssociateID = d.recordnumber
  where si.YearlyRenewal = 1 and acs.Void = 0 and d.StatusId=1 
  and DATEDIFF(DAY, GETDATE(), acs.ExpirationDate) <= 7 and DATEDIFF(DAY, GETDATE(), acs.ExpirationDate) >= 0
  order by expirationdate desc";

                var result = dbConnection.Query<UpcomingServiceExpiryModel>(query);
                return (List<UpcomingServiceExpiryModel>)result;
            }
        }

        public List<ShippingTrackingInfo> GetShippingTrackingInfo()
        {
            using (var dbConnection = new System.Data.SqlClient.SqlConnection(_dataService.GetClientConnectionString().Result))
            {
                var query = $"SELECT ShippingUrl,TrackPattern FROM client.ShippingTrackingUrl";
                var result = dbConnection.Query<ShippingTrackingInfo>(query).ToList();
                return result;
            }
        }
    }
}
