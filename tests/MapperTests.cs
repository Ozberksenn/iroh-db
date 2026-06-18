using Iroh.Models.DTOs.Booking;
using Iroh.Models.DTOs.Child;
using Iroh.Models.DTOs.Company;
using Iroh.Models.DTOs.Customer;
using Iroh.Models.DTOs.Package;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Xunit;

namespace Iroh.Tests
{
    // DTO.From(entity) eşleyicileri — entity sızıntısını engelleyen saf dönüşümler (DB'siz test edilir).
    public class MapperTests
    {
        [Fact]
        public void CustomerDto_From_MapsPublicFieldsOnly()
        {
            var c = new Customer { Id = 5, Name = "Ada", LastName = "Lovelace", Phone = "555", Mail = "a@b.c", IsDeleted = true };
            var dto = CustomerDto.From(c);
            Assert.Equal(5, dto.Id);
            Assert.Equal("Ada", dto.Name);
            Assert.Equal("Lovelace", dto.LastName);
            Assert.Equal("555", dto.Phone);
            Assert.Equal("a@b.c", dto.Mail);
            // CustomerDto'da IsDeleted/CreatedAt yok → sızdırılamaz (derleme zamanı garanti).
        }

        [Fact]
        public void CompanyDto_From_MapsPricing()
        {
            var dto = CompanyDto.From(new Company { Id = 1, Name = "Iroh", FirstHourPrice = 100, AdditionalHalfHourPrice = 50 });
            Assert.Equal(1, dto.Id);
            Assert.Equal("Iroh", dto.Name);
            Assert.Equal(100, dto.FirstHourPrice);
            Assert.Equal(50, dto.AdditionalHalfHourPrice);
        }

        [Fact]
        public void BookingDto_From_SerializesStatusEnumAsString()
        {
            var dto = BookingDto.From(new Booking { Id = 9, TableId = 2, ChildId = 3, Status = BookingStatus.Active, Price = 12.5m });
            Assert.Equal(9, dto.Id);
            Assert.Equal(2, dto.TableId);
            Assert.Equal(3, dto.ChildId);
            Assert.Equal("Active", dto.Status);
            Assert.Equal(12.5m, dto.Price);
        }

        [Fact]
        public void ChildDto_From_MapsWithoutLeakingSoftDelete()
        {
            var birth = new DateTime(2018, 5, 1);
            var dto = ChildDto.From(new Child { Id = 4, ParentId = 1, Name = "Kid", BirthDate = birth, IsDeleted = true });
            Assert.Equal(4, dto.Id);
            Assert.Equal(1, dto.ParentId);
            Assert.Equal("Kid", dto.Name);
            Assert.Equal(birth, dto.BirthDate);
        }

        [Fact]
        public void PackageDto_From_MapsAllPublicFields()
        {
            var dto = PackageDto.From(new Package { Id = 7, Name = "10h", Hours = 10m, Price = 900m, ValidityDays = 30, IsDeleted = true });
            Assert.Equal(7, dto.Id);
            Assert.Equal("10h", dto.Name);
            Assert.Equal(10m, dto.Hours);
            Assert.Equal(900m, dto.Price);
            Assert.Equal(30, dto.ValidityDays);
        }

        [Fact]
        public void BookingLogDto_From_SerializesTypeEnumAsString()
        {
            var t = new DateTime(2026, 1, 2, 3, 4, 5);
            var dto = BookingLogDto.From(new BookingLog { Id = 1, BookingId = 2, Time = t, Type = BookingLogType.Edit, UserId = 8 });
            Assert.Equal(1, dto.Id);
            Assert.Equal(2, dto.BookingId);
            Assert.Equal(t, dto.Time);
            Assert.Equal("Edit", dto.Type);
            Assert.Equal(8, dto.UserId);
        }
    }
}
