namespace AkkeohsVegas.Core
{

    public sealed class SubtitleSegment
    {
        public double StartMs { get; set; }
        public double EndMs { get; set; }
        public string Text { get; set; }

        public double DurationMs
        {
            get { return EndMs > StartMs ? EndMs - StartMs : 0; }
        }

        public SubtitleSegment()
        {
            Text = string.Empty;
        }

        public SubtitleSegment(double startMs, double endMs, string text)
        {
            StartMs = startMs;
            EndMs = endMs;
            Text = text ?? string.Empty;
        }
    }
}
