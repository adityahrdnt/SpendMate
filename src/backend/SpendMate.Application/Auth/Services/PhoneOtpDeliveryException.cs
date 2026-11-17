namespace SpendMate.Application.Auth.Services;

public sealed class PhoneOtpDeliveryException : Exception
{
    public PhoneOtpDeliveryException(Exception innerException)
        : base("Phone OTP delivery failed.", innerException)
    {
    }
}
