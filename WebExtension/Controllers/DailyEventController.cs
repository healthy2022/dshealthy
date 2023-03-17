using DirectScale.Disco.Extension.Middleware;
using DirectScale.Disco.Extension.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;
using WebExtension.Repositories;
using WebExtension.Services;
using WebExtension.ThirdParty.ZiplingoEngagement.Interfaces;

namespace WebExtension.Controllers
{
    [Route("api/webhooks")]
    [ApiController]
    public class DailyEventController : ControllerBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IZiplingoEngagementService _ziplingoEngagementService;
        private readonly IDailyRunService _dailyRunService;
        private readonly ICustomLogRepository _customLogRepository;

        public DailyEventController(ISettingsService settingsService, IZiplingoEngagementService ziplingoEngagementService, IDailyRunService dailyRunService, ICustomLogRepository customLogRepository)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _ziplingoEngagementService = ziplingoEngagementService ?? throw new ArgumentNullException(nameof(ziplingoEngagementService));
            _dailyRunService = dailyRunService ?? throw new ArgumentNullException(nameof(dailyRunService));
            _customLogRepository = customLogRepository ?? throw new ArgumentNullException(nameof(customLogRepository));
        }

        [ExtensionAuthorize]
        [HttpPost("DailyEvent")]
        public async Task<IActionResult> DailyEvent()
        {
            try
            {
                var extensionContext = await _settingsService.ExtensionContext();

                if (extensionContext.EnvironmentType == DirectScale.Disco.Extension.EnvironmentType.Live)
                {
                    try
                    {
                        _ziplingoEngagementService.AssociateBirthDateTrigger();
                        _ziplingoEngagementService.AssociateWorkAnniversaryTrigger();
                        _dailyRunService.FiveDayRun();
                        _dailyRunService.GetAssociateStatuses(); //New sync api for associate statuses
                        _dailyRunService.SentNotificationOnCardExpiryBefore30Days();
                        _dailyRunService.ExecuteCommissionEarned();
                        _ziplingoEngagementService.SevenDaysBeforeAutoshipTrigger();
                        _ziplingoEngagementService.UpcomingServiceExpiry();
                    }
                    catch (Exception ex)
                    {
                        
                    }
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { ex.Message });
            }
        }
    }
}