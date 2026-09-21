using DbConnection.Domain.Entities;
using ReservationWebAPI.Application.Helpers;

namespace ReservationWebAPI.Application.DTOs
{
    public class UpdateReservationStatusRequest
    {
        public Guid ReservationId { get; set; }
        public Reservation.ReservationStatus Status { get; set; }

        public static string? Serialize(UpdateReservationStatusRequest objectToSerialize)
        {
            return JsonHelper.SerializeObjectToJson(objectToSerialize);
        }
        public static UpdateReservationStatusRequest? Deserialize(string objectToDeserialize)
        {
            return JsonHelper.DeserializeJsonToObject<UpdateReservationStatusRequest>(objectToDeserialize);
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
