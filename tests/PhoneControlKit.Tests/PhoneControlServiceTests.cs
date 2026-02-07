using Microsoft.Extensions.Logging;
using Moq;
using PhoneControlKit.Handlers;
using PhoneControlKit.Models;
using System;
using System.Threading.Tasks;
using Xunit;

namespace PhoneControlKit.Tests
{
    public class PhoneControlServiceTests : IDisposable
    {
        private readonly Mock<ILogger<PhoneControlService>> _mockLogger;
        private readonly Mock<IMessageHandler> _mockMessageHandler;
        private readonly Mock<IFileUploadHandler> _mockFileUploadHandler;
        private readonly PhoneControlService _service;

        public PhoneControlServiceTests()
        {
            _mockLogger = new Mock<ILogger<PhoneControlService>>();
            _mockMessageHandler = new Mock<IMessageHandler>();
            _mockFileUploadHandler = new Mock<IFileUploadHandler>();
            
            _service = new PhoneControlService(
                _mockLogger.Object,
                _mockMessageHandler.Object,
                _mockFileUploadHandler.Object
            );
        }

        [Fact]
        public void Constructor_WithValidDependencies_CreatesInstance()
        {
            // Assert
            Assert.NotNull(_service);
            Assert.False(_service.IsRunning);
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new PhoneControlService(null, _mockMessageHandler.Object));
        }

        [Fact]
        public void Constructor_WithNullMessageHandler_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new PhoneControlService(_mockLogger.Object, null));
        }

        [Fact]
        public void Constructor_WithConfiguration_UsesProvidedConfiguration()
        {
            // Arrange
            var config = new ServiceConfiguration { Port = 9999 };

            // Act
            var service = new PhoneControlService(
                _mockLogger.Object,
                _mockMessageHandler.Object,
                null,
                config
            );

            // Assert
            Assert.Equal(9999, service.Port);
            service.Dispose();
        }

        [Fact]
        public void Constructor_WithoutConfiguration_UsesDefaultConfiguration()
        {
            // Assert
            Assert.Equal(8765, _service.Port); // Default port
        }

        [Fact]
        public void GetServerUrl_ReturnsCorrectFormat()
        {
            // Act
            var url = _service.GetServerUrl();

            // Assert
            Assert.Contains("http://", url);
            Assert.Contains(":8765", url);
        }

        public void Dispose()
        {
            _service?.Dispose();
        }
    }
}