using System;
using Novin.Bpmn.EventSourcing.Events;
using Xunit;

namespace Novin.Bpmn.Test.EventSourcing
{
    public class BpmnElementTypeTests
    {
        [Theory]
        [InlineData("bpmn:InclusiveGateway", "InclusiveGateway")]
        [InlineData("bpmn:Imclisuevgateway", "InclusiveGateway")]
        [InlineData("Imclisuevgateway", "InclusiveGateway")]
        [InlineData("bpmn:OrGateway", "InclusiveGateway")]
        [InlineData("inclusive gateway", "InclusiveGateway")]
        [InlineData("inclusive-gateway", "InclusiveGateway")]
        [InlineData("inclusive_gateway", "InclusiveGateway")]
        [InlineData("OR gateway", "InclusiveGateway")]
        [InlineData("bpmn:ExclusiveGateway", "ExclusiveGateway")]
        [InlineData("XOR gateway", "ExclusiveGateway")]
        [InlineData("bpmn:ParallelGateway", "ParallelGateway")]
        [InlineData("AND gateway", "ParallelGateway")]
        [InlineData("bpmn:StartEvent", "StartEvent")]
        [InlineData("start event", "StartEvent")]
        [InlineData("bpmn:EndEvent", "EndEvent")]
        [InlineData("end", "EndEvent")]
        [InlineData("bpmn:UserTask", "UserTask")]
        [InlineData("user task", "UserTask")]
        [InlineData("bpmn:ServiceTask", "ServiceTask")]
        [InlineData("service", "ServiceTask")]
        [InlineData("bpmn:ScriptTask", "ScriptTask")]
        [InlineData("script", "ScriptTask")]
        [InlineData("", "Unknown")]
        [InlineData(null, "Unknown")]
        [InlineData("something invalid", "Unknown")]
        public void FromString_HandlesVariantAndMisspelledTypes(string input, string expected)
        {
            // Act
            var result = BpmnElementType.FromString(input);
            
            // Assert
            Assert.Equal(expected, result.Name);
        }

        [Fact]
        public void ImplicitOperatorFromInt_ReturnsCorrectType()
        {
            // Arrange - using implicit conversion from int to BpmnElementType
            BpmnElementType type1 = 12; // InclusiveGateway's ID
            BpmnElementType type2 = 10; // ExclusiveGateway's ID
            
            // Assert
            Assert.Same(BpmnElementType.InclusiveGateway, type1);
            Assert.Same(BpmnElementType.ExclusiveGateway, type2);
        }
        
        [Fact]
        public void ImplicitOperatorToString_ReturnsName()
        {
            // Arrange
            BpmnElementType type = BpmnElementType.ServiceTask;
            
            // Act
            string name = type;
            
            // Assert
            Assert.Equal("ServiceTask", name);
        }
        
        [Fact]
        public void TypeCheckMethods_ReturnCorrectResults()
        {
            // Arrange
            var userTask = BpmnElementType.UserTask;
            var exclusiveGateway = BpmnElementType.ExclusiveGateway;
            var startEvent = BpmnElementType.StartEvent;
            
            // Assert
            Assert.True(userTask.IsTask());
            Assert.True(userTask.IsUserTask());
            Assert.False(userTask.IsGateway());
            Assert.False(userTask.IsEvent());
            
            Assert.True(exclusiveGateway.IsGateway());
            Assert.True(exclusiveGateway.IsExclusiveGateway());
            Assert.False(exclusiveGateway.IsTask());
            Assert.False(exclusiveGateway.IsEvent());
            
            Assert.True(startEvent.IsEvent());
            Assert.True(startEvent.IsStartEvent());
            Assert.False(startEvent.IsTask());
            Assert.False(startEvent.IsGateway());
        }
        
        [Fact]
        public void Parse_ThrowsException_ForInvalidValues()
        {
            // Assert
            Assert.Throws<ArgumentException>(() => BpmnElementType.Parse("InvalidType"));
        }
        
        [Fact]
        public void TryParse_ReturnsCorrectResult()
        {
            // Act & Assert
            Assert.True(BpmnElementType.TryParse("bpmn:InclusiveGateway", out var type1));
            Assert.Equal(BpmnElementType.InclusiveGateway, type1);
            
            Assert.False(BpmnElementType.TryParse("InvalidType", out var type2));
            Assert.Equal(BpmnElementType.Unknown, type2);
        }
        
        [Fact]
        public void GetAll_ReturnsAllTypes()
        {
            // Act
            var allTypes = BpmnElementType.GetAll();
            
            // Assert
            Assert.Contains(BpmnElementType.StartEvent, allTypes);
            Assert.Contains(BpmnElementType.EndEvent, allTypes);
            Assert.Contains(BpmnElementType.UserTask, allTypes);
            Assert.Contains(BpmnElementType.ServiceTask, allTypes);
            Assert.Contains(BpmnElementType.InclusiveGateway, allTypes);
        }
        
        [Fact]
        public void ToXmlString_ReturnsCorrectFormat()
        {
            // Act
            var xmlString1 = BpmnElementType.UserTask.ToXmlString();
            var xmlString2 = BpmnElementType.Unknown.ToXmlString();
            
            // Assert
            Assert.Equal("bpmn:UserTask", xmlString1);
            Assert.Equal("unknown", xmlString2);
        }
    }
} 