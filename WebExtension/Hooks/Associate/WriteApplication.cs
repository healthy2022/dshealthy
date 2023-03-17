using System;
using System.Threading.Tasks;
using DirectScale.Disco.Extension.Hooks;
using DirectScale.Disco.Extension.Hooks.Associates.Enrollment;
using WebExtension.ThirdParty.ZiplingoEngagement.Interfaces;

namespace WebExtension.Hooks.Associate
{
    public class WriteApplication : IHook<WriteApplicationHookRequest, WriteApplicationHookResponse>
    {
        private readonly IZiplingoEngagementService _ziplingoEngagementService;

        public WriteApplication(IZiplingoEngagementService ziplingoEngagementService)
        {
            _ziplingoEngagementService = ziplingoEngagementService ?? throw new ArgumentNullException(nameof(ziplingoEngagementService));

        }
        public Task<WriteApplicationHookResponse> Invoke(WriteApplicationHookRequest request, Func<WriteApplicationHookRequest, Task<WriteApplicationHookResponse>> func)
        {
            var response = func(request);
            try
            {
                _ziplingoEngagementService.CreateContact(request.Application, response.Result.ApplicationResponse);

            }
            catch (Exception e)
            {
                
            }
            return response;
        }
    }
}
