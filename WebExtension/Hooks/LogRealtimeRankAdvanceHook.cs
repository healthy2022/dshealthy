using System;
using System.Threading.Tasks;
using DirectScale.Disco.Extension.Hooks;
using DirectScale.Disco.Extension.Hooks.Commissions;
using DirectScale.Disco.Extension.Services;
using WebExtension.Repositories;
using WebExtension.Services.ZiplingoEngagementService;

namespace WebExtension.Hooks
{
    public class LogRealtimeRankAdvanceHook : IHook<LogRealtimeRankAdvanceHookRequest, LogRealtimeRankAdvanceHookResponse>
    {
        private readonly IZiplingoEngagementService _ziplingoEngagementService;
        private readonly IAssociateService _associateService;

        public LogRealtimeRankAdvanceHook(IZiplingoEngagementService ziplingoEngagementService, ICustomLogRepository customLogRepository, IAssociateService associateService)
        {
            _ziplingoEngagementService = ziplingoEngagementService ?? throw new ArgumentNullException(nameof(ziplingoEngagementService));
           
            _associateService = associateService ?? throw new ArgumentNullException(nameof(associateService));
        }

        public async Task<LogRealtimeRankAdvanceHookResponse> Invoke(LogRealtimeRankAdvanceHookRequest request, Func<LogRealtimeRankAdvanceHookRequest, Task<LogRealtimeRankAdvanceHookResponse>> func)
        {
            var result = await func(request);
            var associate = await _associateService.GetAssociate(request.AssociateId);
            try
            {
                _ziplingoEngagementService.LogRealtimeRankAdvanceEvent(request);
                _ziplingoEngagementService.UpdateContact(associate);
            }
            catch (Exception ex)
            {
                
            }
            return result;
        }



    }
}
