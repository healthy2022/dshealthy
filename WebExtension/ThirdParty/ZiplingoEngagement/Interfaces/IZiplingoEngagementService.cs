using DirectScale.Disco.Extension;
using System.Collections.Generic;
using WebExtension.ThirdParty.ZiplingoEngagement.Model;
using DirectScale.Disco.Extension.Hooks.Commissions;
using DirectScale.Disco.Extension.Hooks.Associates;

namespace WebExtension.ThirdParty.ZiplingoEngagement.Interfaces
{
    public interface IZiplingoEngagementService
    {
        void CallOrderZiplingoEngagementTrigger(Order order, string eventKey, bool FailedAutoship);
        void CreateEnrollContact(Order order);
        void AssociateBirthDateTrigger();
        void SevenDaysBeforeAutoshipTrigger();
        void CreateContact(Application req, ApplicationResponse response);
        void UpdateContact(Associate req);
        void ResetSettings(CommandRequest commandRequest);
        void SendOrderShippedEmail(int packageId, string trackingNumber);
        void AssociateStatusChangeTrigger(int associateId, int oldStatusId, int newStatusId);
        void AssociateWorkAnniversaryTrigger();
        EmailOnNotificationEvent OnNotificationEvent(NotificationEvent notification);
        LogRealtimeRankAdvanceHookResponse LogRealtimeRankAdvanceEvent(LogRealtimeRankAdvanceHookRequest req);
        void FiveDayRunTrigger(List<AutoshipInfo> autoships);
        void ExpirationCardTrigger(List<CardInfo> cardinfo);
        void SentTRCPointNotification(int orderId, double orderAmount, string emailAddress, string associateId, string description,string orderdate);
        void UpdateAssociateType(int associateId, string oldAssociateType, string newAssociateType, int newAssociateTypeId);
        void UpcomingServiceExpiry();

        void CreateAutoshipTrigger(Autoship autoshipInfo);
        void UpdateAutoshipTrigger(DirectScale.Disco.Extension.Autoship updatedAutoshipInfo);
        void ExecuteCommissionEarned();

    }
}
