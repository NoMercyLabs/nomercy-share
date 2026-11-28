namespace NoMercy.Share.Core.Bar;

/// <summary>A yes-or-no question under the bar: its text, the labels of its two buttons, and what yes and no do.</summary>
public sealed record BarAsk(string Text, string YesLabel, Func<Task> Yes, Action No, string NoLabel);
