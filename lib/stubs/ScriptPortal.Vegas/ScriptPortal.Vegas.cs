using System;
using System.Collections;
using System.Collections.Generic;

namespace ScriptPortal.Vegas
{

    public class Vegas
    {
        public Project Project { get { return null; } }
        public PlugInNode Generators { get { return null; } }
        public void LoadDockView(DockableControl control) { }
        public bool ActivateDockView(string instanceName) { return false; }
        public bool FindDockView(string instanceName) { return false; }
    }

    public class Project
    {
        public Tracks Tracks { get { return null; } }
        public ProjectVideoProperties Video { get { return null; } }
        public VideoTrack AddVideoTrack() { return null; }
        public AudioTrack AddAudioTrack() { return null; }
    }

    public class ProjectVideoProperties
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class Tracks : IEnumerable
    {
        public IEnumerator GetEnumerator() { yield break; }
    }

    public class Track
    {
        public string Name { get; set; }
        public TrackEvents Events { get { return null; } }
        public bool IsVideo() { return false; }
        public bool IsAudio() { return false; }
    }

    public class TrackEvents : IEnumerable
    {
        public IEnumerator GetEnumerator() { yield break; }
    }

    public class VideoTrack : Track
    {
        public VideoEvent AddVideoEvent(Timecode start, Timecode length) { return null; }
    }

    public class AudioTrack : Track
    {
        public AudioEvent AddAudioEvent(Timecode start, Timecode length) { return null; }
    }

    public class TrackEvent
    {
        public bool Selected { get; set; }
        public Timecode Start { get; set; }
        public Timecode Length { get; set; }
        public Timecode End { get; set; }
        public Take ActiveTake { get; set; }
        public Track Track { get { return null; } }
        public MediaType MediaType { get; set; }
        public double PlaybackRate { get; set; }
    }

    public class VideoEvent : TrackEvent
    {
        public Take AddTake(MediaStream stream) { return null; }
        public VideoMotion VideoMotion { get { return null; } }
    }

    public class VideoMotion
    {
        public VideoMotionKeyframes Keyframes { get { return null; } }
    }

    public class VideoMotionKeyframes
    {
        public int Count { get { return 0; } }
        public VideoMotionKeyframe this[int index] { get { return null; } }
    }

    public class VideoMotionKeyframe
    {
        public void MoveBy(VideoMotionVertex amount) { }
        public void ScaleBy(VideoMotionVertex amount) { }
    }

    public class VideoMotionVertex
    {
        public VideoMotionVertex(float x, float y) { X = x; Y = y; }
        public float X { get; set; }
        public float Y { get; set; }
    }

    public class AudioEvent : TrackEvent
    {
        public Take AddTake(MediaStream stream) { return null; }
    }

    public class Take
    {
        public Media Media { get; set; }
        public string MediaPath { get; set; }
        public Timecode Offset { get; set; }
    }

    public class Media
    {
        public Media(PlugInNode generator) { }
        public string FilePath { get; set; }
        public Effect Generator { get; set; }
        public MediaStream GetVideoStreamByIndex(int index) { return null; }
        public MediaStream GetAudioStreamByIndex(int index) { return null; }
    }

    public class MediaStream { }

    public enum MediaType
    {
        Unknown,
        Audio,
        Video
    }

    public class Timecode
    {
        public Timecode() { }
        public Timecode(double nanos) { }
        public double ToMilliseconds() { return 0; }
        public static Timecode FromMilliseconds(double ms) { return new Timecode(); }
        public static Timecode FromSeconds(double seconds) { return new Timecode(); }
    }

    public class PlugInNode
    {
        public string Name { get; set; }
        public PlugInNode GetChildByName(string name) { return null; }
        public PlugInNode FindChildByName(string name) { return null; }
        public IEnumerable Children { get { yield break; } }
    }

    public class Effect
    {
        public string Preset { get; set; }
        public PlugInNode PlugIn { get; set; }
        public OFXEffect OFXEffect { get { return null; } }
    }

    public class OFXEffect
    {
        public OFXParameter FindParameterByName(string name) { return null; }
        public void AllParametersChanged() { }
    }

    public class OFXParameter { }

    public class OFXStringParameter : OFXParameter
    {
        public string Value { get; set; }
    }

    public class OFXDoubleParameter : OFXParameter
    {
        public double Value { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Default { get; set; }
    }

    public struct OFXColor
    {
        public double R;
        public double G;
        public double B;
        public double A;

        public OFXColor(double r, double g, double b)
        {
            R = r; G = g; B = b; A = 1.0;
        }

        public OFXColor(double r, double g, double b, double a)
        {
            R = r; G = g; B = b; A = a;
        }
    }

    public class OFXRGBAParameter : OFXParameter
    {
        public OFXColor Value { get; set; }
        public OFXColor Default { get; set; }
    }

    public sealed class UndoBlock : IDisposable
    {
        public UndoBlock(string name) { }
        public UndoBlock(Project project, string name) { }
        public void Dispose() { }
    }

    public enum CommandCategory
    {
        Tools,
        View,
        Edit,
        None
    }

    public class CustomCommand
    {
        public CustomCommand(CommandCategory category, string name) { }
        public string DisplayName { get; set; }
        public bool Checked { get; set; }
        public event EventHandler Invoked;
        public event EventHandler MenuPopup;
    }

    public interface ICustomCommandModule
    {
        ICollection GetCustomCommands();
        void InitializeModule(Vegas vegas);
    }

    public interface IDockView
    {
    }

    public enum DockWindowStyle
    {
        Detached,
        Floating,
        Docked
    }

    public class DockableControl : System.Windows.Forms.UserControl, IDockView
    {
        public DockableControl(string windowTitle)
        {
            Name = windowTitle;
            InstanceName = windowTitle;
            DisplayName = windowTitle;
        }

        public string InstanceName { get; private set; }
        public string DisplayName { get; set; }
        public bool PersistDockWindowState { get; set; }

        public virtual DockWindowStyle DefaultDockWindowStyle
        {
            get { return DockWindowStyle.Floating; }
            set { }
        }

        public virtual System.Drawing.Size DefaultFloatingSize
        {
            get { return new System.Drawing.Size(320, 240); }
            set { }
        }

        protected virtual bool OnClosed()
        {
            return true;
        }
    }
}
