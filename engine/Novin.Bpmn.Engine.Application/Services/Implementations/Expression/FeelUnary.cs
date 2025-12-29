namespace Novin.Bpmn.Engine.Application.Services;

internal sealed record FeelUnary(string Op, FeelNode Expr) : FeelNode;