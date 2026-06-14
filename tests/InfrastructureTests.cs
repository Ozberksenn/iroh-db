using Iroh.Infrastructure;
using Iroh.Models.Responses;
using Xunit;

namespace Iroh.Tests
{
    public class KebabCaseParameterTransformerTests
    {
        private readonly KebabCaseParameterTransformer _t = new();

        [Theory]
        [InlineData("BookingLog", "booking-log")]
        [InlineData("PurchasePayment", "purchase-payment")]
        [InlineData("Customer", "customer")]
        [InlineData("Auth", "auth")]
        public void TransformOutbound_KebabCasesPascalRouteTokens(string input, string expected)
        {
            Assert.Equal(expected, _t.TransformOutbound(input));
        }

        [Fact]
        public void TransformOutbound_Null_ReturnsNull()
        {
            Assert.Null(_t.TransformOutbound(null));
        }
    }

    public class ApiResponseTests
    {
        [Fact]
        public void Ok_WrapsDataAsSuccessEnvelope()
        {
            var r = ApiResponse.Ok(42, "tamam");
            Assert.True(r.Success);
            Assert.Equal(42, r.Data);
            Assert.Equal("tamam", r.Message);
        }

        [Fact]
        public void Ok_AllowsNullPayload()
        {
            var r = ApiResponse.Ok<object?>(null, "silindi");
            Assert.True(r.Success);
            Assert.Null(r.Data);
            Assert.Equal("silindi", r.Message);
        }
    }
}
