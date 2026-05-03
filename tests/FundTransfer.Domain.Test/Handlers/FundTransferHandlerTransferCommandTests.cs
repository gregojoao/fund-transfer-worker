using FundTransfer.Domain.Bus.Publishers;
using FundTransfer.Domain.Commands;
using FundTransfer.Domain.Entities;
using FundTransfer.Domain.Enums;
using FundTransfer.Domain.Handlers;
using FundTransfer.Domain.Repositories;
using FundTransfer.Domain.Services.Contracts;
using Moq;
using NUnit.Framework;

namespace FundTransfer.Domain.Test.Handlers
{
    partial class FundTransferHandlerTransferCommandTests
    {
        private const string INVALID_ACCOUNT_NUMBER = "Invalid account number";
        private const string INSUFFICIENT_FUNDS = "Insufficient funds";
        private FundTransferHandler _handler;
        private readonly Mock<ITransferRepository> _transferRepository = new();
        private readonly Mock<IBusPublisher> _busPublisher = new();

        [SetUp]
        public void Setup()
        {
            var accountService = new Mock<IAccountService>();
            _handler = new FundTransferHandler(accountService.Object, _transferRepository.Object, _busPublisher.Object);
        }

        [Test]
        [TestCase("a4d4edbb-f9a9-4a59-9398-ed24ecb0dbfc", 0, "123", "321", 0.1f)]
        [Category("/Handlers/FundTransferHandler/TransferCommand")]
        public async Task AccountNotFound(Guid transactionId, TransferStatusEnum transferStatusEnum, string accountOrigin,
            string accountDestination, float? value)
        {
            var accountService = new Mock<IAccountService>();
            accountService.Setup(x => x.CheckAccountAsync(accountOrigin, default, transactionId))
                .Returns(Task.FromResult(false));
            accountService.Setup(x => x.CheckAccountAsync(accountDestination, default, transactionId))
                .Returns(Task.FromResult(false));
            _handler = new FundTransferHandler(accountService.Object, _transferRepository.Object, _busPublisher.Object);

            var right = new TransferCommand(transactionId, transferStatusEnum, accountOrigin, accountDestination, value);
            var commandResult = await _handler.Handle(right, default);
            var transfer = (Transfer)commandResult.Data;
            Assert.That(commandResult.Sucess, Is.True);
            Assert.That(transfer.TransferStatus, Is.EqualTo(TransferStatusEnum.Error));
            Assert.That(transfer.Message, Is.EqualTo(INVALID_ACCOUNT_NUMBER));
        }

        [Test]
        [TestCase("a4d4edbb-f9a9-4a59-9398-ed24ecb0dbfc", 0, "123", "321", 0.1f)]
        [Category("/Handlers/FundTransferHandler/TransferCommand")]
        public async Task Insufficientfunds(Guid transactionId, TransferStatusEnum transferStatusEnum, string accountOrigin,
            string accountDestination, float? value)
        {
            var accountService = new Mock<IAccountService>();
            accountService.Setup(x => x.CheckAccountAsync(accountOrigin, default, transactionId))
                .Returns(Task.FromResult(true));
            accountService.Setup(x => x.CheckAccountAsync(accountDestination, default, transactionId))
                .Returns(Task.FromResult(true));
            accountService.Setup(x => x.CheckBalanceAsync(accountOrigin, default, transactionId))
                .Returns(Task.FromResult((float?)0f));
            _handler = new FundTransferHandler(accountService.Object, _transferRepository.Object, _busPublisher.Object);

            var right = new TransferCommand(transactionId, transferStatusEnum, accountOrigin, accountDestination, value);
            var commandResult = await _handler.Handle(right, default);
            var transfer = (Transfer)commandResult.Data;
            Assert.That(commandResult.Sucess, Is.True);
            Assert.That(transfer.TransferStatus, Is.EqualTo(TransferStatusEnum.Error));
            Assert.That(transfer.Message, Is.EqualTo(INSUFFICIENT_FUNDS));
        }

        [Test]
        [TestCase("a4d4edbb-f9a9-4a59-9398-ed24ecb0dbfc", 0, "123", "321", 0.1f)]
        [Category("/Handlers/FundTransferHandler/TransferCommand")]
        public async Task IncompleteTransfer(Guid transactionId, TransferStatusEnum transferStatusEnum, string accountOrigin,
            string accountDestination, float? value)
        {
            var accountService = new Mock<IAccountService>();
            accountService.Setup(x => x.CheckAccountAsync(accountOrigin, default, transactionId))
                .Returns(Task.FromResult(true));
            accountService.Setup(x => x.CheckAccountAsync(accountDestination, default, transactionId))
                .Returns(Task.FromResult(true));
            accountService.Setup(x => x.CheckBalanceAsync(accountOrigin, default, transactionId))
                .Returns(Task.FromResult((float?)100f));
            bool errorOccurred = true;
            accountService.Setup(x => x.TransferAsync(accountOrigin, accountDestination, (float)value, out errorOccurred, default, transactionId))
                .Returns(Task.FromResult(false));
            _handler = new FundTransferHandler(accountService.Object, _transferRepository.Object, _busPublisher.Object);

            var right = new TransferCommand(transactionId, transferStatusEnum, accountOrigin, accountDestination, value);
            var commandResult = await _handler.Handle(right, default);
            var transfer = (Transfer)commandResult.Data;
            Assert.That(commandResult.Sucess, Is.True);
            Assert.That(transfer.TransferStatus, Is.EqualTo(TransferStatusEnum.Processing));
            Assert.That(transfer.Message, Is.EqualTo(""));
        }

        [Test]
        [TestCase("a4d4edbb-f9a9-4a59-9398-ed24ecb0dbfc", 0, "123", "321", 0.1f)]
        [Category("/Handlers/FundTransferHandler/TransferCommand")]
        public async Task NoTransfer(Guid transactionId, TransferStatusEnum transferStatusEnum, string accountOrigin,
            string accountDestination, float? value)
        {
            var accountService = new Mock<IAccountService>();
            accountService.Setup(x => x.CheckAccountAsync(accountOrigin, default, transactionId))
                .Returns(Task.FromResult(true));
            accountService.Setup(x => x.CheckAccountAsync(accountDestination, default, transactionId))
                .Returns(Task.FromResult(true));
            accountService.Setup(x => x.CheckBalanceAsync(accountOrigin, default, transactionId))
                .Returns(Task.FromResult((float?)100f));
            bool errorOccurred = false;
            accountService.Setup(x => x.TransferAsync(accountOrigin, accountDestination, (float)value, out errorOccurred, default, transactionId))
                .Returns(Task.FromResult(false));
            _handler = new FundTransferHandler(accountService.Object, _transferRepository.Object, _busPublisher.Object);

            var right = new TransferCommand(transactionId, transferStatusEnum, accountOrigin, accountDestination, value);
            var commandResult = await _handler.Handle(right, default);
            var transfer = (Transfer)commandResult.Data;
            Assert.That(commandResult.Sucess, Is.False);
            Assert.That(transfer.TransferStatus, Is.EqualTo(TransferStatusEnum.InQueue));
            Assert.That(transfer.Message, Is.EqualTo(""));
        }

        [Test]
        [TestCase("a4d4edbb-f9a9-4a59-9398-ed24ecb0dbfc", 0, "123", "321", 0.1f)]
        [Category("/Handlers/FundTransferHandler/TransferCommand")]
        public async Task TransferConfirmed(Guid transactionId, TransferStatusEnum transferStatusEnum, string accountOrigin,
            string accountDestination, float? value)
        {
            var accountService = new Mock<IAccountService>();
            accountService.Setup(x => x.CheckAccountAsync(accountOrigin, default, transactionId))
                .Returns(Task.FromResult(true));
            accountService.Setup(x => x.CheckAccountAsync(accountDestination, default, transactionId))
                .Returns(Task.FromResult(true));
            accountService.Setup(x => x.CheckBalanceAsync(accountOrigin, default, transactionId))
                .Returns(Task.FromResult((float?)100f));
            bool errorOccurred = true;
            accountService.Setup(x => x.TransferAsync(accountOrigin, accountDestination, (float)value, out errorOccurred, default, transactionId))
                .Returns(Task.FromResult(true));
            _handler = new FundTransferHandler(accountService.Object, _transferRepository.Object, _busPublisher.Object);

            var right = new TransferCommand(transactionId, transferStatusEnum, accountOrigin, accountDestination, value);
            var commandResult = await _handler.Handle(right, default);
            var transfer = (Transfer)commandResult.Data;
            Assert.That(commandResult.Sucess, Is.True);
            Assert.That(transfer.TransferStatus, Is.EqualTo(TransferStatusEnum.Confirmed));
            Assert.That(transfer.Message, Is.EqualTo(""));
        }
    }
}
