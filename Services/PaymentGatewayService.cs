namespace MessManagementSystem.Services
{
    public static class PaymentGatewayService
    {
        /// <summary>
        /// Simulates a dummy payment gateway for processing credit/debit card payments
        /// </summary>
        public static PaymentResult ProcessPayment(string cardNumber, string cardHolderName, string expiryDate, string cvv, decimal amount)
        {
            // Simulate payment processing delay
            Thread.Sleep(1000);

            // Basic validation
            if (string.IsNullOrWhiteSpace(cardNumber) || cardNumber.Replace(" ", "").Length != 16)
            {
                return new PaymentResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Invalid card number. Must be 16 digits."
                };
            }

            if (string.IsNullOrWhiteSpace(cardHolderName))
            {
                return new PaymentResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Card holder name is required."
                };
            }

            if (string.IsNullOrWhiteSpace(expiryDate) || !IsValidExpiryDate(expiryDate))
            {
                return new PaymentResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Invalid expiry date. Use MM/YY format."
                };
            }

            if (string.IsNullOrWhiteSpace(cvv) || cvv.Length < 3 || cvv.Length > 4)
            {
                return new PaymentResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Invalid CVV. Must be 3 or 4 digits."
                };
            }

            // Simulate random failures (5% chance)
            var random = new Random();
            if (random.Next(100) < 5)
            {
                return new PaymentResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Transaction declined by bank. Please try again or use a different card."
                };
            }

            // Generate transaction ID
            var transactionId = $"TXN{DateTime.Now:yyyyMMddHHmmss}{random.Next(1000, 9999)}";

            return new PaymentResult
            {
                IsSuccess = true,
                TransactionId = transactionId,
                ProcessedAmount = amount,
                ProcessedDate = DateTime.Now,
                SuccessMessage = "Payment processed successfully!"
            };
        }

        /// <summary>
        /// Generates a unique payment token for secure transaction tracking
        /// </summary>
        public static string GeneratePaymentToken()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();
        }

        private static bool IsValidExpiryDate(string expiryDate)
        {
            if (string.IsNullOrWhiteSpace(expiryDate))
                return false;

            var parts = expiryDate.Split('/');
            if (parts.Length != 2)
                return false;

            if (!int.TryParse(parts[0], out int month) || !int.TryParse(parts[1], out int year))
                return false;

            if (month < 1 || month > 12)
                return false;

            // Assume YY format, convert to full year
            if (year < 100)
                year += 2000;

            var expiryDateValue = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
            return expiryDateValue >= DateTime.Today;
        }
    }

    public class PaymentResult
    {
        public bool IsSuccess { get; set; }
        public string? TransactionId { get; set; }
        public decimal ProcessedAmount { get; set; }
        public DateTime ProcessedDate { get; set; }
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
