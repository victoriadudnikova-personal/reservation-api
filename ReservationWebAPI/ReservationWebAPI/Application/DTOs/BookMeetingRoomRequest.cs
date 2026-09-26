using Azure.Core;
using ReservationWebAPI.Application.Helpers;
namespace ReservationWebAPI.Application.DTOs
{
    public class BookMeetingRoomRequest
    {
        public Guid MeetingRoomId { get; set; }
        public int ReservationDurationInMinutes {  get; set; }
        public DateTimeOffset StartAt { get; set; }

        public static string? Serialize(BookMeetingRoomRequest objectToSerialize)
        {
            return JsonHelper.SerializeObjectToJson(objectToSerialize);
        }
        public static BookMeetingRoomRequest? Deserialize(string objectToDeserialize)
        {
            return JsonHelper.DeserializeJsonToObject<BookMeetingRoomRequest>(objectToDeserialize);
        }

        public (string?, OperationResponse?) CheckIfCanBeSerialized()
        {
            var requestHash = Serialize(this);
            if (string.IsNullOrEmpty(requestHash))
            {
                return (null, new OperationResponse()
                {
                    ResponseMessage = "Failed to serialize request to save in the idempotency record.",
                    StatusCode = StatusCodes.Status400BadRequest
                });
            }
            return (requestHash, null);
        }
    }
}
