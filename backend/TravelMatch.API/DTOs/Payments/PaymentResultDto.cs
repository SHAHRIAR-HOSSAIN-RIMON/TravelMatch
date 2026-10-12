namespace TravelMatch.API.DTOs.Payments;

public class PaymentResultDto
{
    public bool Success { get; set; }

    public PaymentError Error { get; set; } = PaymentError.None;

    public string Message { get; set; } = string.Empty;

    public PaymentResponseDto? Data { get; set; }
}
