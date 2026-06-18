using Iroh.Exceptions;
using Iroh.Models.DTOs.Wallet;
using Iroh.Models.Responses;
using Iroh.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Iroh.Controllers
{
    // Cüzdan + iki defter API'si (docs/wallet-redesign.md). Purchase/PurchasePayment uçlarının yerini alır.
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class WalletController : ControllerBase
    {
        private readonly IWalletService _wallet;
        private readonly IPackageService _packageService;

        public WalletController(IWalletService wallet, IPackageService packageService)
        {
            _wallet = wallet;
            _packageService = packageService;
        }

        // JWT'deki "id" claim'i (AuthService bu şekilde basıyor).
        private int? CurrentUserId() =>
            int.TryParse(User.FindFirst("id")?.Value, out var id) ? id : (int?)null;

        [HttpGet("{customerId}")]
        public async Task<IActionResult> Get(int customerId)
        {
            var wallet = await _wallet.GetWallet(customerId);
            return Ok(ApiResponse.Ok(wallet, "Başarılı"));
        }

        [HttpPost("credit")]
        public async Task<IActionResult> Credit(WalletCreditDto dto)
        {
            int minutes;
            decimal money;
            DateTime? validFrom = dto.ValidFrom;
            DateTime? validTo = dto.ValidTo;

            if (dto.PackageId.HasValue)
            {
                var pkg = await _packageService.GetById(dto.PackageId.Value)
                    ?? throw new NotFoundException("Paket bulunamadı!");
                minutes = (int)Math.Round(pkg.Hours * 60);
                money = pkg.Price;
                validFrom ??= DateTime.UtcNow;
                if (pkg.ValidityDays.HasValue && !validTo.HasValue)
                    validTo = validFrom.Value.AddDays(pkg.ValidityDays.Value);
            }
            else
            {
                if (!dto.Minutes.HasValue)
                    throw new BusinessRuleException("Süre (minutes) veya paket (packageId) belirtilmelidir.");
                minutes = dto.Minutes.Value;
                money = dto.Money ?? 0m;
            }

            var wallet = await _wallet.CreditTime(dto.CustomerId, minutes, money, dto.PackageId, CurrentUserId(), validFrom, validTo);
            return Ok(ApiResponse.Ok(wallet, "Kredi eklendi"));
        }

        [HttpPost("settle")]
        public async Task<IActionResult> Settle(WalletSettleDto dto)
        {
            var wallet = await _wallet.Settle(dto.CustomerId, dto.Amount, CurrentUserId());
            return Ok(ApiResponse.Ok(wallet, "Tahsilat alındı"));
        }

        [HttpPost("adjust")]
        public async Task<IActionResult> Adjust(WalletAdjustDto dto)
        {
            if (dto.Minutes.HasValue)
            {
                var w = await _wallet.AdjustTime(dto.CustomerId, dto.Minutes.Value, dto.Reason, CurrentUserId());
                return Ok(ApiResponse.Ok(w, "Süre düzeltmesi uygulandı"));
            }
            if (dto.Amount.HasValue)
            {
                var w = await _wallet.AdjustCash(dto.CustomerId, dto.Amount.Value, dto.Reason, CurrentUserId());
                return Ok(ApiResponse.Ok(w, "Tutar düzeltmesi uygulandı"));
            }
            throw new BusinessRuleException("Düzeltme için minutes veya amount verilmelidir.");
        }
    }
}
