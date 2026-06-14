using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Iroh.Infrastructure
{
    // [controller]/[action] token'larını kebab-case'e çevirir: BookingLog -> booking-log, PurchasePayment -> purchase-payment.
    public class KebabCaseParameterTransformer : IOutboundParameterTransformer
    {
        public string? TransformOutbound(object? value)
        {
            if (value == null) return null;
            return Regex.Replace(value.ToString()!, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant();
        }
    }
}
