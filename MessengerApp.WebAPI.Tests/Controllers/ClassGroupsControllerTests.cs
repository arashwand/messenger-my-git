using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using MessengerApp.WebAPI.Controllers;
using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Messenger.API.Controllers;

namespace MessengerApp.WebAPI.Tests.Controllers
{
    public class ClassGroupsControllerTests
    {
        [Fact]
        public async Task UpsertClassGroupFromPortal_ReturnsOk_OnSuccess()
        {
            // Arrange
            var mockService = new Mock<IClassGroupService>();
            mockService
                .Setup(s => s.UpsertClassGroupFromModelAsync(It.IsAny<ClassGroupModel>()))
                .Returns(Task.CompletedTask);

            var mockLogger = new Mock<ILogger<ClassGroupsController>>();

            var controller = new ClassGroupsController(mockService.Object, mockLogger.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                        new Claim(ClaimTypes.NameIdentifier, "123")
                    }, "TestAuth"))
                }
            };

            var model = new ClassGroupModel
            {
                ClassId = 1,
                LevelName = "Level",
                TeacherUserId = 2,
                ClassTiming = "10:00",
                TeacherName = "Teacher",
                IsActive = true,
                EndDate = DateTime.UtcNow
                // Members intentionally left unset; service is mocked
            };

            // Act
            var result = await controller.UpsertClassGroupFromPortal(model);

            // Assert
            Assert.IsType<OkResult>(result);
            mockService.Verify(s => s.UpsertClassGroupFromModelAsync(It.IsAny<ClassGroupModel>()), Times.Once);
        }

        [Fact]
        public async Task UpsertClassGroupFromPortal_ReturnsBadRequest_OnServiceException()
        {
            // Arrange
            var mockService = new Mock<IClassGroupService>();
            mockService
                .Setup(s => s.UpsertClassGroupFromModelAsync(It.IsAny<ClassGroupModel>()))
                .ThrowsAsync(new Exception("failure"));

            var mockLogger = new Mock<ILogger<ClassGroupsController>>();

            var controller = new ClassGroupsController(mockService.Object, mockLogger.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                        new Claim(ClaimTypes.NameIdentifier, "123")
                    }, "TestAuth"))
                }
            };

            var model = new ClassGroupModel
            {
                ClassId = 1,
                LevelName = "Level",
                TeacherUserId = 2,
                ClassTiming = "10:00",
                TeacherName = "Teacher",
                IsActive = true,
                EndDate = DateTime.UtcNow
            };

            // Act
            var result = await controller.UpsertClassGroupFromPortal(model);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("failure", badRequest.Value);
            mockService.Verify(s => s.UpsertClassGroupFromModelAsync(It.IsAny<ClassGroupModel>()), Times.Once);
        }

        [Fact]
        public async Task UpsertClassGroupFromPortal_ReturnsUnauthorized_WhenUserNotAuthenticated()
        {
            // Arrange
            var mockService = new Mock<IClassGroupService>();
            var mockLogger = new Mock<ILogger<ClassGroupsController>>();
            var controller = new ClassGroupsController(mockService.Object, mockLogger.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity()) // no claims
                }
            };

            var model = new ClassGroupModel { ClassId = 1, ClassTiming = "timing" };

            // Act
            var result = await controller.UpsertClassGroupFromPortal(model);

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task UpsertClassGroupFromPortal_CallsService_AndReturnsOk_WhenAuthenticated()
        {
            // Arrange
            var mockService = new Mock<IClassGroupService>();
            mockService.Setup(s => s.UpsertClassGroupFromModelAsync(It.IsAny<ClassGroupModel>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var mockLogger = new Mock<ILogger<ClassGroupsController>>();
            var controller = new ClassGroupsController(mockService.Object, mockLogger.Object);

            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "42") };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };

            var model = new ClassGroupModel { ClassId = 2, ClassTiming = "timing" };

            // Act
            var result = await controller.UpsertClassGroupFromPortal(model);

            // Assert
            Assert.IsType<OkResult>(result);
            mockService.Verify(s => s.UpsertClassGroupFromModelAsync(It.Is<ClassGroupModel>(m => m.ClassId == model.ClassId)), Times.Once);
        }

        [Fact]
        public async Task UpsertClassGroupFromPortal_AllowsPortalClaim_CallsService()
        {
            // Arrange
            var mockService = new Mock<IClassGroupService>();
            mockService.Setup(s => s.UpsertClassGroupFromModelAsync(It.IsAny<ClassGroupModel>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var mockLogger = new Mock<ILogger<ClassGroupsController>>();
            var controller = new ClassGroupsController(mockService.Object, mockLogger.Object);

            var claims = new[] {
                    new Claim(ClaimTypes.NameIdentifier, "99"),
                    new Claim("scope", "system_bot") // portal account scope required by policy
                };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };

            var model = new ClassGroupModel { ClassId = 5, ClassTiming = "t" };

            // Act
            var result = await controller.UpsertClassGroupFromPortal(model);

            // Assert
            Assert.IsType<OkResult>(result);
            mockService.Verify(s => s.UpsertClassGroupFromModelAsync(It.IsAny<ClassGroupModel>()), Times.Once);
        }

        [Fact]
        public async Task UpsertClassGroupFromPortal_ReturnsBadRequest_WhenServiceThrows()
        {
            // Arrange
            var mockService = new Mock<IClassGroupService>();
            mockService.Setup(s => s.UpsertClassGroupFromModelAsync(It.IsAny<ClassGroupModel>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            var mockLogger = new Mock<ILogger<ClassGroupsController>>();
            var controller = new ClassGroupsController(mockService.Object, mockLogger.Object);

            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "7") };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };

            var model = new ClassGroupModel { ClassId = 3, ClassTiming = "timing" };

            // Act
            var result = await controller.UpsertClassGroupFromPortal(model);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("boom", bad.Value);

            // verify logging of error
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
