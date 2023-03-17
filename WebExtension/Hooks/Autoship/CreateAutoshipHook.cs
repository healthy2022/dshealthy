using System;
using System.Threading.Tasks;
using DirectScale.Disco.Extension.Hooks;
using DirectScale.Disco.Extension.Hooks.Autoships;
using DirectScale.Disco.Extension.Services;
using WebExtension.ThirdParty.ZiplingoEngagement.Interfaces;

namespace WebExtension.Hooks.Autoship
{
    public class CreateAutoshipHook : IHook<CreateAutoshipHookRequest, CreateAutoshipHookResponse>
    {
        private readonly IZiplingoEngagementService _ziplingoEngagementService;
        private readonly IAssociateService _associateService;
        private readonly IAutoshipService _autoshipService;

        public CreateAutoshipHook(IZiplingoEngagementService ziplingoEngagementService, IAssociateService associateService, IAutoshipService autoshipService)
        {
            _ziplingoEngagementService = ziplingoEngagementService;
            _associateService = associateService;
            _autoshipService = autoshipService;
        }

        public async Task<CreateAutoshipHookResponse> Invoke(CreateAutoshipHookRequest request, Func<CreateAutoshipHookRequest, Task<CreateAutoshipHookResponse>> func)
        {
            var result = await func(request);
            try
            {
                var autoshipInfo = await _autoshipService.GetAutoship(result.AutoshipId);
                _ziplingoEngagementService.CreateAutoshipTrigger(autoshipInfo);
                var associateSummary = await _associateService.GetAssociate(autoshipInfo.AssociateId);
                _ziplingoEngagementService.UpdateContact(associateSummary);
            }
            catch (Exception ex)
            {

            }
            return result;
        }
    }
}
