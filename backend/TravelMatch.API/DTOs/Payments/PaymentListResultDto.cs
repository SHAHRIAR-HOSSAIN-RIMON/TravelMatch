namespace TravelMatch.API.DTOs.Payments;

public class PaymentListResultDto
{
    public bool Success { get; set; }

    public PaymentError Error { get; set; } = PaymentError.None;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<PaymentResponseDto> Data { get; set; } = Array.Empty<PaymentResponseDto>();

    public int TotalCount { get; set; }
}
