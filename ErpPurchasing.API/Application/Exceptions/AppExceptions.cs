namespace ErpPurchasing.API.Application.Exceptions;

// Tüm uygulama hatalarının ortak atası
public abstract class AppException : Exception
{
    public abstract int StatusCode { get; }
    public abstract string Title { get; }

    protected AppException(string message) : base(message) { }
}

// 404: Aranan kayıt yok
public sealed class NotFoundException : AppException
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public override string Title => "Kayıt bulunamadı";
    public NotFoundException(string message) : base(message) { }
}

// 409: Mevcut veriyle çakışma (aynı ürün kodu, aynı irsaliye no vb.)
public sealed class ConflictException : AppException
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public override string Title => "Çakışma";
    public ConflictException(string message) : base(message) { }
}

// 422: İstek formatı doğru ama iş kuralına aykırı (fazla teslimat, iptal edilmiş sipariş vb.)
public sealed class BusinessRuleException : AppException
{
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
    public override string Title => "İş kuralı ihlali";
    public BusinessRuleException(string message) : base(message) { }
}
