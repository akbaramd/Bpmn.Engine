using Novin.Bpmn.Engine.Application;
using Novin.Bpmn.Engine.Infrastructure;
using Novin.Bpmn.Engine.Api.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add BPMN Engine services
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var app = builder.Build();


// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Demo: Test BPMN Engine
await RunDemoAsync(app.Services);

app.Run();

static async Task RunDemoAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("=== BPMN Engine Demo Started ===");

        // 1. Deploy a process
      logger.LogInformation("1. Deploying process...");

var bpmnXmlInclusive = """
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
  id="Definitions_inclusive_demo"
  targetNamespace="http://novin-bpmn/demo">

  <bpmn:process id="inclusive-demo" name="Inclusive Gateway Demo" isExecutable="true">

    <bpmn:startEvent id="start" name="Start">
      <bpmn:outgoing>f_start_to_init</bpmn:outgoing>
    </bpmn:startEvent>

    <bpmn:scriptTask id="init" name="Init" scriptFormat="javascript">
      <bpmn:incoming>f_start_to_init</bpmn:incoming>
      <bpmn:outgoing>f_init_to_or_split</bpmn:outgoing>
      <bpmn:script><![CDATA[
        // Initialize variables, such as amount and customerType
        // Example:
        // variables["amount"] = 120;
        // variables["customerType"] = "VIP";
      ]]></bpmn:script>
    </bpmn:scriptTask>

    <!-- OR-SPLIT (Inclusive Gateway) -->
    <bpmn:inclusiveGateway id="or_split" name="OR Split" default="f_or_default_to_join">
      <bpmn:incoming>f_init_to_or_split</bpmn:incoming>
      <bpmn:outgoing>f_or_to_a</bpmn:outgoing>
      <bpmn:outgoing>f_or_to_b</bpmn:outgoing>
      <bpmn:outgoing>f_or_default_to_join</bpmn:outgoing>
    </bpmn:inclusiveGateway>

    <!-- Script A -->
    <bpmn:scriptTask id="scriptA" name="Script A" scriptFormat="javascript">
      <bpmn:incoming>f_or_to_a</bpmn:incoming>
      <bpmn:outgoing>f_a_to_or_join</bpmn:outgoing>
      <bpmn:script><![CDATA[
        // do work A
        // Example: variables["didA"] = true;
      ]]></bpmn:script>
    </bpmn:scriptTask>

    <!-- Script B -->
    <bpmn:scriptTask id="scriptB" name="Script B" scriptFormat="javascript">
      <bpmn:incoming>f_or_to_b</bpmn:incoming>
      <bpmn:outgoing>f_b_to_or_join</bpmn:outgoing>
      <bpmn:script><![CDATA[
        // do work B
        // Example: variables["didB"] = true;
      ]]></bpmn:script>
    </bpmn:scriptTask>

    <!-- OR-JOIN -->
    <bpmn:inclusiveGateway id="or_join" name="OR Join">
      <bpmn:incoming>f_a_to_or_join</bpmn:incoming>
      <bpmn:incoming>f_b_to_or_join</bpmn:incoming>
      <bpmn:incoming>f_or_default_to_join</bpmn:incoming>
      <bpmn:outgoing>f_or_join_to_end</bpmn:outgoing>
    </bpmn:inclusiveGateway>

    <!-- End Event -->
    <bpmn:endEvent id="end" name="End">
      <bpmn:incoming>f_or_join_to_end</bpmn:incoming>
    </bpmn:endEvent>

    <!-- Sequence Flows -->
    <bpmn:sequenceFlow id="f_start_to_init" sourceRef="start" targetRef="init"/>
    <bpmn:sequenceFlow id="f_init_to_or_split" sourceRef="init" targetRef="or_split"/>

    <!-- Conditional Sequence Flows -->
    <bpmn:sequenceFlow id="f_or_to_a" sourceRef="or_split" targetRef="scriptA">
      <bpmn:conditionExpression xsi:type="bpmn:tFormalExpression"
        language="https://www.omg.org/spec/FEEL/1.1">amount &gt; 100</bpmn:conditionExpression>
    </bpmn:sequenceFlow>

    <bpmn:sequenceFlow id="f_or_to_b" sourceRef="or_split" targetRef="scriptB">
      <bpmn:conditionExpression xsi:type="bpmn:tFormalExpression"
        language="https://www.omg.org/spec/FEEL/1.1">customerType = "VIP"</bpmn:conditionExpression>
    </bpmn:sequenceFlow>

    <!-- Default path if no conditions match -->
    <bpmn:sequenceFlow id="f_or_default_to_join" sourceRef="or_split" targetRef="or_join"/>

    <bpmn:sequenceFlow id="f_a_to_or_join" sourceRef="scriptA" targetRef="or_join"/>
    <bpmn:sequenceFlow id="f_b_to_or_join" sourceRef="scriptB" targetRef="or_join"/>

    <bpmn:sequenceFlow id="f_or_join_to_end" sourceRef="or_join" targetRef="end"/>

  </bpmn:process>
</bpmn:definitions>

""";


        var deployCommand = new Novin.Bpmn.Engine.Application.Commands.DeployProcess.DeployProcessCommand(
            "demo-process-key",
            bpmnXmlInclusive,
            "Demo Process Deployment");
        
        var deployResult = await mediator.Send(deployCommand);
        
        logger.LogInformation("   ✓ Process deployed. DeploymentId: {DeploymentId}, Version: {Version}", 
            deployResult.DeploymentId, deployResult.Version);

        // 2. Start a process instance
        logger.LogInformation("2. Starting process instance...");
        var startCommand = new Novin.Bpmn.Engine.Application.Commands.StartProcess.StartProcessCommand(
            "demo-process-key",
            "Demo Process Instance",
            new Dictionary<string, object> { { "amount", 1000 } });
        
        var startResult = await mediator.Send(startCommand);
        
        logger.LogInformation("   ✓ Process started. ProcessId: {ProcessId}", startResult.ProcessId);

       


    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in demo");
    }
}

