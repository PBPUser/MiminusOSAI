using System.Reflection;

namespace Miminus.Sys;

/// <summary>Speaks a line of text aloud.
///
/// The system's own audio is synthesised from oscillators and cannot form
/// words, so this reaches for the speech engine Windows already has. SAPI is
/// addressed through late-bound COM rather than a package reference, which
/// keeps the project's rule — nothing but the platform — intact: if the engine
/// or a suitable voice is missing, <see cref="Available"/> is false and the
/// caller falls back to something it can make itself.</summary>
public static class Speech
{
    const int Async = 1;          // SVSFlagsAsync
    const int PurgeBeforeSpeak = 2;

    static readonly object Gate = new();
    static Type _type;
    static object _voice;
    static bool _tried;

    /// <summary>Description of the voice in use, for the UI to show.</summary>
    public static string VoiceName { get; private set; }

    /// <summary>True when the selected voice can actually read Cyrillic. When
    /// it cannot, a caller should hand over a transliteration instead.</summary>
    public static bool SpeaksRussian { get; private set; }

    public static bool Available
    {
        get { lock (Gate) { Init(); return _voice != null; } }
    }

    /// <summary>Speaks <paramref name="cyrillic"/> when the chosen voice reads
    /// Russian, and the transliteration when it does not — an English voice
    /// given Cyrillic either spells it out or says nothing at all.</summary>
    public static bool SayName(string cyrillic, string transliterated)
    {
        bool _ = Available;      // resolves the voice, and with it SpeaksRussian
        return Say(SpeaksRussian ? cyrillic : transliterated);
    }

    /// <summary>Speaks asynchronously, cutting off anything already being said.
    /// Returns false when no speech engine could be reached.</summary>
    public static bool Say(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        lock (Gate)
        {
            Init();
            if (_voice == null) return false;

            try
            {
                _type.InvokeMember("Speak", BindingFlags.InvokeMethod, null, _voice,
                                   new object[] { text, Async | PurgeBeforeSpeak });
                return true;
            }
            catch
            {
                // A voice can disappear mid-session (a device change, a policy);
                // drop it so the next call tries again from scratch.
                _voice = null;
                _tried = false;
                return false;
            }
        }
    }

    static void Init()
    {
        if (_tried) return;
        _tried = true;

        try
        {
            _type = Type.GetTypeFromProgID("SAPI.SpVoice");
            if (_type == null) return;

            _voice = Activator.CreateInstance(_type);
            SelectVoice();
        }
        catch { _voice = null; }
    }

    /// <summary>Prefers a Russian voice when one is installed, since everything
    /// this speaks is a Russian name.</summary>
    static void SelectVoice()
    {
        try
        {
            object tokens = _type.InvokeMember("GetVoices", BindingFlags.InvokeMethod, null,
                                               _voice, new object[] { "", "" });
            var listType = tokens.GetType();
            int count = (int)listType.InvokeMember("Count", BindingFlags.GetProperty, null, tokens, null);

            object best = null;
            string bestName = null;

            for (int i = 0; i < count; i++)
            {
                object token = listType.InvokeMember("Item", BindingFlags.InvokeMethod, null,
                                                     tokens, new object[] { i });
                string name = (string)token.GetType().InvokeMember(
                    "GetDescription", BindingFlags.InvokeMethod, null, token, new object[] { 0 });

                if (best == null) { best = token; bestName = name; }

                if (name != null &&
                    (name.Contains("Rus", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("Русск", StringComparison.OrdinalIgnoreCase)))
                {
                    best = token;
                    bestName = name;
                    SpeaksRussian = true;
                    break;
                }
            }

            if (best != null)
            {
                _type.InvokeMember("Voice", BindingFlags.SetProperty, null, _voice, new[] { best });
                VoiceName = bestName;
            }
        }
        catch
        {
            // The default voice will do; only the preference failed.
        }
    }
}
