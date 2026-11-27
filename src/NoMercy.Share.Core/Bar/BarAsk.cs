namespace NoMercy.Share.Core.Bar;

/// <summary>A yes-or-no question under the bar: its text, the label of its yes button, and what yes and no do.</summary>
public sealed record BarAsk(string Text, string YesLabel, Func<Task> Yes, Action No);
