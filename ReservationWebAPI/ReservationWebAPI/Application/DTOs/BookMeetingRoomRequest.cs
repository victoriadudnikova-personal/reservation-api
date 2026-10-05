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

        public string? CheckIfCanBeSerialized()
        {
            var requestHash = Serialize(this);
            if (string.IsNullOrEmpty(requestHash))
            {
                throw new CustomException("Failed to serialize request to save in the idempotency record.", CustomException.ExceptionType.SerializationError);
            }
            return requestHash;
        }
    }
}
