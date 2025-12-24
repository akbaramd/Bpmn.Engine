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

var bpmnXmlEnterprise = """
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
  xmlns:bonyan="http://bonyan.org/schema/bpmn/1.0"
  id="Definitions_enterprise_demo"
  targetNamespace="http://novin-bpmn/demo">

  <bpmn:process id="enterprise-demo" name="Enterprise Workflow Gateway Test" isExecutable="true">

    <!-- 1) Start -->
    <bpmn:startEvent id="start" name="Start">
      <bpmn:outgoing>f_start_to_init</bpmn:outgoing>
    </bpmn:startEvent>

    <!-- 2) Init (JS) -->
    <bpmn:scriptTask id="init" name="Init Variables (JS)" scriptFormat="javascript">
      <bonyan:ioMapping onMissingSource="skip" onMissingOutput="skip" overwrite="true">
        <bonyan:output source="amount" target="amount"/>
        <bonyan:output source="customerType" target="customerType"/>
        <bonyan:output source="country" target="country"/>
        <bonyan:output source="riskScore" target="riskScore"/>
        <bonyan:output source="managerApproved" target="managerApproved"/>
        <bonyan:output source="inventoryAvailable" target="inventoryAvailable"/>
        <bonyan:output source="trace" target="trace"/>
        <bonyan:output source="initAt" target="initAt"/>
      </bonyan:ioMapping>

      <bpmn:incoming>f_start_to_init</bpmn:incoming>
      <bpmn:outgoing>f_init_to_xor_amount</bpmn:outgoing>

      <bpmn:script><![CDATA[
        // Local variables only (engine should treat context.Variables as token-local)
        context.Variables["amount"] = 120;
        context.Variables["customerType"] = "VIP";
        context.Variables["country"] = "IR";
        context.Variables["riskScore"] = 65;

        context.Variables["managerApproved"] = true;
        context.Variables["inventoryAvailable"] = true;

        context.Variables["trace"] = "init;";
        context.Variables["initAt"] = "js";

        log("[init] amount=" + context.Variables["amount"]
          + ", customerType=" + context.Variables["customerType"]
          + ", country=" + context.Variables["country"]
          + ", riskScore=" + context.Variables["riskScore"]);
      ]]></bpmn:script>
    </bpmn:scriptTask>

    <!-- 3) XOR Split based on amount -->
    <bpmn:exclusiveGateway id="xor_amount" name="XOR: Amount Routing" default="f_xor_default_to_high">
      <bonyan:ioMapping onMissingSource="null">
        <bonyan:input source="amount" target="amount"/>
      </bonyan:ioMapping>

      <bpmn:incoming>f_init_to_xor_amount</bpmn:incoming>
      <bpmn:outgoing>f_xor_to_fast</bpmn:outgoing>
      <bpmn:outgoing>f_xor_to_high</bpmn:outgoing>
      <bpmn:outgoing>f_xor_default_to_high</bpmn:outgoing>
    </bpmn:exclusiveGateway>

    <!-- fast path -->
    <bpmn:scriptTask id="fastTrack" name="Fast Track (JS)" scriptFormat="javascript">
      <bonyan:ioMapping onMissingSource="null" onMissingOutput="skip" overwrite="true">
        <bonyan:input source="amount" target="amount"/>
        <bonyan:input source="trace" target="trace"/>
        <bonyan:output source="path" target="path"/>
        <bonyan:output source="requiresApproval" target="requiresApproval"/>
        <bonyan:output source="riskScore" target="riskScore"/>
        <bonyan:output source="trace" target="trace"/>
      </bonyan:ioMapping>

      <bpmn:incoming>f_xor_to_fast</bpmn:incoming>
      <bpmn:outgoing>f_fast_to_and_split</bpmn:outgoing>

      <bpmn:script><![CDATA[
        context.Variables["path"] = "FAST";
        context.Variables["requiresApproval"] = false;
        context.Variables["riskScore"] = 20;
        context.Variables["trace"] = (context.Variables["trace"] || "") + "fastTrack;";
        log("[fastTrack] path=FAST, riskScore=" + context.Variables["riskScore"]);
      ]]></bpmn:script>
    </bpmn:scriptTask>

    <!-- high value path -->
    <bpmn:scriptTask id="highValue" name="High Value Handling (C#)" scriptFormat="C#">
      <bonyan:ioMapping onMissingSource="null" onMissingOutput="skip" overwrite="true">
        <bonyan:input source="amount" target="amount"/>
        <bonyan:input source="riskScore" target="riskScore"/>
        <bonyan:input source="trace" target="trace"/>
        <bonyan:output source="path" target="path"/>
        <bonyan:output source="requiresApproval" target="requiresApproval"/>
        <bonyan:output source="riskScore" target="riskScore"/>
        <bonyan:output source="trace" target="trace"/>
      </bonyan:ioMapping>

      <bpmn:incoming>f_xor_to_high</bpmn:incoming>
      <bpmn:outgoing>f_high_to_and_split</bpmn:outgoing>

      <bpmn:script><![CDATA[
        context.Variables["path"] = "HIGH";
        context.Variables["requiresApproval"] = true;

        var rsObj = context.Variables["riskScore"];
        var rs = rsObj is long l ? l
               : rsObj is int i ? i
               : rsObj is double d ? (long)d
               : Convert.ToInt64(rsObj);

        context.Variables["riskScore"] = rs + 10;
        context.Variables["trace"] = (context.Variables.Contains("trace") ? context.Variables["trace"] : "") + "highValue;";

        Console.WriteLine($"[highValue] path=HIGH, requiresApproval=true, riskScore now={context.Variables["riskScore"]}");
      ]]></bpmn:script>
    </bpmn:scriptTask>

    <!-- default -> high -->
    <bpmn:sequenceFlow id="f_xor_default_to_high" sourceRef="xor_amount" targetRef="highValue"/>

    <!-- 4) AND (Parallel) Split for checks -->
    <bpmn:parallelGateway id="and_split_checks" name="AND Split: Checks">
      <bpmn:incoming>f_fast_to_and_split</bpmn:incoming>
      <bpmn:incoming>f_high_to_and_split</bpmn:incoming>
      <bpmn:outgoing>f_and_to_fraud</bpmn:outgoing>
      <bpmn:outgoing>f_and_to_inventory</bpmn:outgoing>
    </bpmn:parallelGateway>

    <!-- Fraud check (C#) -->
    <bpmn:scriptTask id="fraudCheck" name="Fraud Check (C#)" scriptFormat="C#">
      <bonyan:ioMapping onMissingSource="fail" onMissingOutput="skip" overwrite="true">
        <bonyan:input source="amount" target="amount"/>
        <bonyan:input source="riskScore" target="riskScore"/>
        <bonyan:input source="trace" target="trace"/>
        <bonyan:output source="didFraudCheck" target="didFraudCheck"/>
        <bonyan:output source="fraudFlag" target="fraudFlag"/>
        <bonyan:output source="trace" target="trace"/>
      </bonyan:ioMapping>

      <bpmn:incoming>f_and_to_fraud</bpmn:incoming>
      <bpmn:outgoing>f_fraud_to_and_join</bpmn:outgoing>

      <bpmn:script><![CDATA[
        var amountObj = context.Variables["amount"];
        var amount = amountObj is long l ? l
                   : amountObj is int i ? i
                   : amountObj is double d ? (long)d
                   : Convert.ToInt64(amountObj);

        var rsObj = context.Variables["riskScore"];
        var risk = rsObj is long rl ? rl
                 : rsObj is int ri ? ri
                 : rsObj is double rd ? (long)rd
                 : Convert.ToInt64(rsObj);

        var fraud = (risk >= 80) || (amount > 500);

        context.Variables["didFraudCheck"] = true;
        context.Variables["fraudFlag"] = fraud;
        context.Variables["trace"] = (context.Variables.Contains("trace") ? context.Variables["trace"] : "") + "fraudCheck;";

        Console.WriteLine($"[fraudCheck] didFraudCheck=true, fraudFlag={fraud}, amount={amount}, riskScore={risk}");
      ]]></bpmn:script>
    </bpmn:scriptTask>

    <!-- Inventory check (JS) -->
    <bpmn:scriptTask id="inventoryCheck" name="Inventory Check (JS)" scriptFormat="javascript">
      <bonyan:ioMapping onMissingSource="null" onMissingOutput="skip" overwrite="true">
        <bonyan:input source="inventoryAvailable" target="inventoryAvailable"/>
        <bonyan:input source="trace" target="trace"/>
        <bonyan:output source="didInventoryCheck" target="didInventoryCheck"/>
        <bonyan:output source="inventoryOk" target="inventoryOk"/>
        <bonyan:output source="trace" target="trace"/>
      </bonyan:ioMapping>

      <bpmn:incoming>f_and_to_inventory</bpmn:incoming>
      <bpmn:outgoing>f_inventory_to_and_join</bpmn:outgoing>

      <bpmn:script><![CDATA[
        context.Variables["didInventoryCheck"] = true;
        context.Variables["inventoryOk"] = (context.Variables["inventoryAvailable"] === true);
        context.Variables["trace"] = (context.Variables["trace"] || "") + "inventoryCheck;";
        log("[inventoryCheck] inventoryOk=" + context.Variables["inventoryOk"]);
      ]]></bpmn:script>
    </bpmn:scriptTask>

    <!-- AND Join -->
    <bpmn:parallelGateway id="and_join_checks" name="AND Join: Checks">
      <bpmn:incoming>f_fraud_to_and_join</bpmn:incoming>
      <bpmn:incoming>f_inventory_to_and_join</bpmn:incoming>
      <bpmn:outgoing>f_and_join_to_or_split</bpmn:outgoing>
    </bpmn:parallelGateway>

    <!-- 5) OR (Inclusive) Split for enrichment -->
    <bpmn:inclusiveGateway id="or_split_enrich" name="OR Split: Enrich" default="f_or_default_to_or_join">
      <bonyan:ioMapping onMissingSource="null">
        <bonyan:input source="customerType" target="customerType"/>
        <bonyan:input source="country" target="country"/>
        <bonyan:input source="riskScore" target="riskScore"/>
      </bonyan:ioMapping>

      <bpmn:incoming>f_and_join_to_or_split</bpmn:incoming>
      <bpmn:outgoing>f_or_to_discount</bpmn:outgoing>
      <bpmn:outgoing>f_or_to_compliance</bpmn:outgoing>
      <bpmn:outgoing>f_or_default_to_or_join</bpmn:outgoing>
    </bpmn:inclusiveGateway>

    <!-- VIP discount (C#) -->
    <bpmn:scriptTask id="vipDiscount" name="VIP Discount (C#)" scriptFormat="C#">
      <bonyan:ioMapping onMissingSource="fail" onMissingOutput="skip" overwrite="true">
        <bonyan:input source="amount" target="amount"/>
        <bonyan:input source="trace" target="trace"/>
        <bonyan:output source="discountApplied" target="discountApplied"/>
        <bonyan:output source="discountRate" target="discountRate"/>
        <bonyan:output source="totalAfterDiscount" target="totalAfterDiscount"/>
        <bonyan:output source="trace" target="trace"/>
      </bonyan:ioMapping>

      <bpmn:incoming>f_or_to_discount</bpmn:incoming>
      <bpmn:outgoing>f_discount_to_or_join</bpmn:outgoing>

      <bpmn:script><![CDATA[
        context.Variables["discountApplied"] = true;
        context.Variables["discountRate"] = 0.10;

        var amountObj = context.Variables["amount"];
        var amount = amountObj is long l ? l
                   : amountObj is int i ? i
                   : amountObj is double d ? (long)d
                   : Convert.ToInt64(amountObj);

        var total = amount - (amount * 10 / 100);

        context.Variables["totalAfterDiscount"] = total;
        context.Variables["trace"] = (context.Variables.Contains("trace") ? context.Variables["trace"] : "") + "vipDiscount;";

        Console.WriteLine($"[vipDiscount] discountApplied=true, totalAfterDiscount={total}");
      ]]></bpmn:script>
    </bpmn:scriptTask>

    <!-- Compliance review (JS) -->
    <bpmn:scriptTask id="complianceReview" name="Compliance Review (JS)" scriptFormat="javascript">
      <bonyan:ioMapping onMissingSource="null" onMissingOutput="skip" overwrite="true">
        <bonyan:input source="country" target="country"/>
        <bonyan:input source="trace" target="trace"/>
        <bonyan:output source="complianceOk" target="complianceOk"/>
        <bonyan:output source="trace" target="trace"/>
      </bonyan:ioMapping>

      <bpmn:incoming>f_or_to_compliance</bpmn:incoming>
      <bpmn:outgoing>f_compliance_to_or_join</bpmn:outgoing>

      <bpmn:script><![CDATA[
        var country = context.Variables["country"];
        var ok = true;

        ok = true;

        context.Variables["complianceOk"] = ok;
        context.Variables["trace"] = (context.Variables["trace"] || "") + "complianceReview;";
        log("[complianceReview] complianceOk=" + context.Variables["complianceOk"] + ", country=" + country);
      ]]></bpmn:script>
    </bpmn:scriptTask>

    <!-- OR Join -->
    <bpmn:inclusiveGateway id="or_join_enrich" name="OR Join: Enrich">
      <bpmn:incoming>f_discount_to_or_join</bpmn:incoming>
      <bpmn:incoming>f_compliance_to_or_join</bpmn:incoming>
      <bpmn:incoming>f_or_default_to_or_join</bpmn:incoming>
      <bpmn:outgoing>f_or_join_to_xor_decision</bpmn:outgoing>
    </bpmn:inclusiveGateway>

    <!-- 6) XOR Decision -->
    <bpmn:exclusiveGateway id="xor_decision" name="XOR: Final Decision" default="f_xor_default_to_reject">
      <bonyan:ioMapping onMissingSource="null">
        <bonyan:input source="inventoryOk" target="inventoryOk"/>
        <bonyan:input source="fraudFlag" target="fraudFlag"/>
        <bonyan:input source="complianceOk" target="complianceOk"/>
        <bonyan:input source="requiresApproval" target="requiresApproval"/>
        <bonyan:input source="managerApproved" target="managerApproved"/>
      </bonyan:ioMapping>

      <bpmn:incoming>f_or_join_to_xor_decision</bpmn:incoming>
      <bpmn:outgoing>f_xor_to_approve</bpmn:outgoing>
      <bpmn:outgoing>f_xor_to_manual</bpmn:outgoing>
      <bpmn:outgoing>f_xor_default_to_reject</bpmn:outgoing>
    </bpmn:exclusiveGateway>

    <bpmn:scriptTask id="approve" name="Approve (C#)" scriptFormat="C#">
      <bonyan:ioMapping onMissingSource="null" onMissingOutput="skip" overwrite="true">
        <bonyan:input source="trace" target="trace"/>
        <bonyan:output source="decision" target="decision"/>
        <bonyan:output source="trace" target="trace"/>
      </bonyan:ioMapping>

      <bpmn:incoming>f_xor_to_approve</bpmn:incoming>
      <bpmn:outgoing>f_approve_to_print</bpmn:outgoing>

      <bpmn:script><![CDATA[
        context.Variables["decision"] = "APPROVED";
        context.Variables["trace"] = (context.Variables.Contains("trace") ? context.Variables["trace"] : "") + "approve;";
        Console.WriteLine("[approve] decision=APPROVED");
      ]]></bpmn:script>
    </bpmn:scriptTask>

    <bpmn:scriptTask id="manualReview" name="Manual Review (JS)" scriptFormat="javascript">
      <bonyan:ioMapping onMissingSource="null" onMissingOutput="skip" overwrite="true">
        <bonyan:input source="trace" target="trace"/>
        <bonyan:output source="decision" target="decision"/>
        <bonyan:output source="trace" target="trace"/>
      </bonyan:ioMapping>

      <bpmn:incoming>f_xor_to_manual</bpmn:incoming>
      <bpmn:outgoing>f_manual_to_print</bpmn:outgoing>

      <bpmn:script><![CDATA[
        context.Variables["decision"] = "MANUAL_REVIEW";
        context.Variables["trace"] = (context.Variables["trace"] || "") + "manualReview;";
        log("[manualReview] decision=MANUAL_REVIEW");
      ]]></bpmn:script>
    </bpmn:scriptTask>

    <bpmn:scriptTask id="reject" name="Reject (C#)" scriptFormat="C#">
      <bonyan:ioMapping onMissingSource="null" onMissingOutput="skip" overwrite="true">
        <bonyan:input source="trace" target="trace"/>
        <bonyan:output source="decision" target="decision"/>
        <bonyan:output source="trace" target="trace"/>
      </bonyan:ioMapping>

      <bpmn:incoming>f_xor_default_to_reject</bpmn:incoming>
      <bpmn:outgoing>f_reject_to_print</bpmn:outgoing>

      <bpmn:script><![CDATA[
        context.Variables["decision"] = "REJECTED";
        context.Variables["trace"] = (context.Variables.Contains("trace") ? context.Variables["trace"] : "") + "reject;";
        Console.WriteLine("[reject] decision=REJECTED");
      ]]></bpmn:script>
    </bpmn:scriptTask>

    <!-- 7) Print Result (C#) -->
    <bpmn:scriptTask id="printResult" name="Print Result (C#)" scriptFormat="C#">
      <bonyan:ioMapping onMissingSource="null">
        <bonyan:input source="amount" target="amount"/>
        <bonyan:input source="customerType" target="customerType"/>
        <bonyan:input source="country" target="country"/>
        <bonyan:input source="riskScore" target="riskScore"/>
        <bonyan:input source="path" target="path"/>
        <bonyan:input source="requiresApproval" target="requiresApproval"/>
        <bonyan:input source="managerApproved" target="managerApproved"/>
        <bonyan:input source="didFraudCheck" target="didFraudCheck"/>
        <bonyan:input source="fraudFlag" target="fraudFlag"/>
        <bonyan:input source="didInventoryCheck" target="didInventoryCheck"/>
        <bonyan:input source="inventoryOk" target="inventoryOk"/>
        <bonyan:input source="discountApplied" target="discountApplied"/>
        <bonyan:input source="discountRate" target="discountRate"/>
        <bonyan:input source="totalAfterDiscount" target="totalAfterDiscount"/>
        <bonyan:input source="complianceOk" target="complianceOk"/>
        <bonyan:input source="decision" target="decision"/>
        <bonyan:input source="trace" target="trace"/>
        <bonyan:input source="initAt" target="initAt"/>
      </bonyan:ioMapping>

      <bpmn:incoming>f_approve_to_print</bpmn:incoming>
      <bpmn:incoming>f_manual_to_print</bpmn:incoming>
      <bpmn:incoming>f_reject_to_print</bpmn:incoming>
      <bpmn:outgoing>f_print_to_end</bpmn:outgoing>

      <bpmn:script><![CDATA[
        object? Get(string k) => context.Variables.Contains(k) ? context.Variables[k] : null;

        Console.WriteLine("========================================");
        Console.WriteLine("[C# printResult] FINAL VARIABLES (LOCAL)");
        Console.WriteLine($"amount              = {Get("amount")}");
        Console.WriteLine($"customerType        = {Get("customerType")}");
        Console.WriteLine($"country             = {Get("country")}");
        Console.WriteLine($"riskScore           = {Get("riskScore")}");
        Console.WriteLine($"path                = {Get("path")}");
        Console.WriteLine($"requiresApproval     = {Get("requiresApproval")}");
        Console.WriteLine($"managerApproved      = {Get("managerApproved")}");
        Console.WriteLine($"didFraudCheck        = {Get("didFraudCheck")}");
        Console.WriteLine($"fraudFlag            = {Get("fraudFlag")}");
        Console.WriteLine($"didInventoryCheck    = {Get("didInventoryCheck")}");
        Console.WriteLine($"inventoryOk          = {Get("inventoryOk")}");
        Console.WriteLine($"discountApplied      = {Get("discountApplied")}");
        Console.WriteLine($"discountRate         = {Get("discountRate")}");
        Console.WriteLine($"totalAfterDiscount   = {Get("totalAfterDiscount")}");
        Console.WriteLine($"complianceOk         = {Get("complianceOk")}");
        Console.WriteLine($"decision             = {Get("decision")}");
        Console.WriteLine($"trace                = {Get("trace")}");
        Console.WriteLine($"initAt               = {Get("initAt")}");
        Console.WriteLine("========================================");
      ]]></bpmn:script>
    </bpmn:scriptTask>

    <bpmn:endEvent id="end" name="End">
      <bpmn:incoming>f_print_to_end</bpmn:incoming>
    </bpmn:endEvent>

    <!-- Sequence Flows -->
    <bpmn:sequenceFlow id="f_start_to_init" sourceRef="start" targetRef="init"/>
    <bpmn:sequenceFlow id="f_init_to_xor_amount" sourceRef="init" targetRef="xor_amount"/>

    <bpmn:sequenceFlow id="f_xor_to_fast" sourceRef="xor_amount" targetRef="fastTrack">
      <bpmn:conditionExpression xsi:type="bpmn:tFormalExpression" language="https://www.omg.org/spec/FEEL/1.1">
        amount &lt;= 100
      </bpmn:conditionExpression>
    </bpmn:sequenceFlow>

    <bpmn:sequenceFlow id="f_xor_to_high" sourceRef="xor_amount" targetRef="highValue">
      <bpmn:conditionExpression xsi:type="bpmn:tFormalExpression" language="https://www.omg.org/spec/FEEL/1.1">
        amount &gt; 100
      </bpmn:conditionExpression>
    </bpmn:sequenceFlow>

    <bpmn:sequenceFlow id="f_fast_to_and_split" sourceRef="fastTrack" targetRef="and_split_checks"/>
    <bpmn:sequenceFlow id="f_high_to_and_split" sourceRef="highValue" targetRef="and_split_checks"/>

    <bpmn:sequenceFlow id="f_and_to_fraud" sourceRef="and_split_checks" targetRef="fraudCheck"/>
    <bpmn:sequenceFlow id="f_and_to_inventory" sourceRef="and_split_checks" targetRef="inventoryCheck"/>

    <bpmn:sequenceFlow id="f_fraud_to_and_join" sourceRef="fraudCheck" targetRef="and_join_checks"/>
    <bpmn:sequenceFlow id="f_inventory_to_and_join" sourceRef="inventoryCheck" targetRef="and_join_checks"/>

    <bpmn:sequenceFlow id="f_and_join_to_or_split" sourceRef="and_join_checks" targetRef="or_split_enrich"/>

    <bpmn:sequenceFlow id="f_or_to_discount" sourceRef="or_split_enrich" targetRef="vipDiscount">
      <bpmn:conditionExpression xsi:type="bpmn:tFormalExpression" language="https://www.omg.org/spec/FEEL/1.1">
        customerType = "VIP"
      </bpmn:conditionExpression>
    </bpmn:sequenceFlow>

    <bpmn:sequenceFlow id="f_or_to_compliance" sourceRef="or_split_enrich" targetRef="complianceReview">
      <bpmn:conditionExpression xsi:type="bpmn:tFormalExpression" language="https://www.omg.org/spec/FEEL/1.1">
        country = "IR" or riskScore &gt;= 70
      </bpmn:conditionExpression>
    </bpmn:sequenceFlow>

    <bpmn:sequenceFlow id="f_or_default_to_or_join" sourceRef="or_split_enrich" targetRef="or_join_enrich"/>
    <bpmn:sequenceFlow id="f_discount_to_or_join" sourceRef="vipDiscount" targetRef="or_join_enrich"/>
    <bpmn:sequenceFlow id="f_compliance_to_or_join" sourceRef="complianceReview" targetRef="or_join_enrich"/>

    <bpmn:sequenceFlow id="f_or_join_to_xor_decision" sourceRef="or_join_enrich" targetRef="xor_decision"/>

    <bpmn:sequenceFlow id="f_xor_to_approve" sourceRef="xor_decision" targetRef="approve">
      <bpmn:conditionExpression xsi:type="bpmn:tFormalExpression" language="https://www.omg.org/spec/FEEL/1.1">
        inventoryOk = true and fraudFlag != true and (complianceOk = true or complianceOk = null) and (requiresApproval != true or managerApproved = true)
      </bpmn:conditionExpression>
    </bpmn:sequenceFlow>

    <bpmn:sequenceFlow id="f_xor_to_manual" sourceRef="xor_decision" targetRef="manualReview">
      <bpmn:conditionExpression xsi:type="bpmn:tFormalExpression" language="https://www.omg.org/spec/FEEL/1.1">
        requiresApproval = true and managerApproved != true
      </bpmn:conditionExpression>
    </bpmn:sequenceFlow>

    <bpmn:sequenceFlow id="f_xor_default_to_reject" sourceRef="xor_decision" targetRef="reject"/>

    <bpmn:sequenceFlow id="f_approve_to_print" sourceRef="approve" targetRef="printResult"/>
    <bpmn:sequenceFlow id="f_manual_to_print" sourceRef="manualReview" targetRef="printResult"/>
    <bpmn:sequenceFlow id="f_reject_to_print" sourceRef="reject" targetRef="printResult"/>

    <bpmn:sequenceFlow id="f_print_to_end" sourceRef="printResult" targetRef="end"/>

  </bpmn:process>
</bpmn:definitions>

""";




        var deployCommand = new Novin.Bpmn.Engine.Application.Commands.DeployProcess.DeployProcessCommand(
            "demo-process-key",
            bpmnXmlEnterprise,
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

