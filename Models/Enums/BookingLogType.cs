namespace Iroh.Models.Enums
{
    public enum BookingLogType
    {
        Create,
        Cancel,
        Complete,
        Pause,
        Continue,
        Edit // DB'de mevcut (bookinglogs.type); enum'da eksikti.
    }
}
