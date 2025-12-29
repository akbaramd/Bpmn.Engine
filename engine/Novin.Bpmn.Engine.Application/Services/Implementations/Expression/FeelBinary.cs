namespace Novin.Bpmn.Engine.Application.Services;

internal sealed record FeelBinary(string Op, FeelNode Left, FeelNode Right) : FeelNode;