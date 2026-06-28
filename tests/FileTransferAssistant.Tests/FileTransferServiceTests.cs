using Microsoft.Extensions.Logging;
using Moq;
using FileTransferAssistant.Handlers;
using FileTransferAssistant.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace FileTransferAssistant.Tests
{
    public class FileTransferServiceTests : IDisposable
    {
        private readonly Mock<ILogger<FileTransferService>> _mockLogger;
        private readonly Mock<IMessageHandler> _mockMessageHandler;
        private readonly Mock<IFileUploadHandler> _mockFileUploadHandler;
        private readonly FileTransferService _service;

        public FileTransferServiceTests()
        {
            _mockLogger = new Mock<ILogger<FileTransferService>>();
            _mockMessageHandler = new Mock<IMessageHandler>();
            _mockFileUploadHandler = new Mock<IFileUploadHandler>();
            
            _service = new FileTransferService(
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
                new FileTransferService(null, _mockMessageHandler.Object));
        }

        [Fact]
        public void Constructor_WithNullMessageHandler_UsesDefaultHandler()
        {
            // Arrange & Act & Assert
            using var service = new FileTransferService(_mockLogger.Object, null);
            Assert.NotNull(service);
        }

        [Fact]
        public void Constructor_WithConfiguration_UsesProvidedConfiguration()
        {
            // Arrange
            var config = new ServiceConfiguration { Port = 9999 };

            // Act
            var service = new FileTransferService(
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
        public void Constructor_WithConfiguration_ExposesStorageDirectory()
        {
            // Arrange
            var config = new ServiceConfiguration
            {
                StorageDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
            };

            // Act
            using var service = new FileTransferService(
                _mockLogger.Object,
                _mockMessageHandler.Object,
                _mockFileUploadHandler.Object,
                config
            );

            // Assert
            Assert.Equal(config.StorageDirectory, service.StorageDirectory);
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
