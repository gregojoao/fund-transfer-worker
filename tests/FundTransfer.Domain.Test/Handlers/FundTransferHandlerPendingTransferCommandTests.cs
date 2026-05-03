using FundTransfer.Domain.Bus.Publishers;
using FundTransfer.Domain.Commands;
using FundTransfer.Domain.Entities;
using FundTransfer.Domain.Enums;
using FundTransfer.Domain.Handlers;
using FundTransfer.Domain.Repositories;
using FundTransfer.Domain.Services;
using FundTransfer.Domain.Services.Contracts;
using Moq;
using NUnit.Framework;

namespace FundTransfer.Domain.Test.Handlers
{
    partial class FundTransferHandlerPendingTransferCommandTests
    {
        private static readonly string ERROR_OCCURRED = "An error has occurred";
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
        [TestCase("a4d4edbb-f9a9-4a59-9398-ed24ecb0dbfc", "123", 0.1f, 0)]
        [Category("/Handlers/FundTransferHandler/PendingTransferCommand")]
        public async Task TransferNotReversed(Guid transactionId, string account, float value, TransferTypeEnum transferType)
        {
            var accountService = new Mock<IAccountService>();
            accountService.Setup(x => x.Reversal(account, value, transferType, default, transactionId))
                .Returns(Task.FromResult(false));
            _handler = new FundTransferHandler(accountService.Object, _transferRepository.Object, _busPublisher.Object);

            var right = new PendingTransferCommand(transactionId, account, value, transferType);
            var commandResult = await _handler.Handle(right, default);
            var transfer = (Transfer)commandResult.Data;
            Assert.That(commandResult.Sucess, Is.False);
            Assert.That(commandResult.Message, Is.EqualTo(ERROR_OCCURRED));
        }
        
        [Test]
        [TestCase("a4d4edbb-f9a9-4a59-9398-ed24ecb0dbfc", "123", 0.1f, 0)]
        [Category("/Handlers/FundTransferHandler/PendingTransferCommand")]
        public async Task ReversalTransfer(Guid transactionId, string account, float value, TransferTypeEnum transferType)
        {
            var accountService = new Mock<IAccountService>();
            accountService.Setup(x => x.Reversal(account, value, transferType, default, transactionId))
                .Returns(Task.FromResult(true));
            var transferDto = new Transfer(transactionId, TransferStatusEnum.Confirmed, account, "123", value);
            _transferRepository.Setup(x => x.GetAsync(transactionId, default))
                .Returns(Task.FromResult(transferDto));
            _handler = new FundTransferHandler(accountService.Object, _transferRepository.Object, _busPublisher.Object);

            var right = new PendingTransferCommand(transactionId, account, value, transferType);
            var commandResult = await _handler.Handle(right, default);
            var transfer = (Transfer)commandResult.Data;
            Assert.That(commandResult.Sucess, Is.True);
            Assert.That(transfer.TransferStatus, Is.EqualTo(TransferStatusEnum.Confirmed));
            Assert.That(transfer.Message, Is.EqualTo(""));
        }
    }
}