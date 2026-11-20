// Wirtschaft-Mod v1 – Speichern und Laden als lesbare XML-Datei im Spielstand-Ordner
using System;
using System.IO;
using System.Xml.Serialization;

namespace Wirtschaft.Kern
{
    public static class Speicher
    {
        public const string Dateiname = "Wirtschaft.xml";
        static readonly XmlSerializer Serializer = new XmlSerializer(typeof(Stand));

        public static Stand Laden(string ordner)
        {
            string pfad = Path.Combine(ordner, Dateiname);
            if (!File.Exists(pfad)) return new Stand();
            using (var s = File.OpenRead(pfad))
                return (Stand)Serializer.Deserialize(s);
        }

        /// <summary>Schreibt zuerst in eine Temp-Datei und hält eine .bak-Kopie, damit bei einem Absturz nichts verloren geht.</summary>
        public static void Speichern(Stand stand, string ordner)
        {
            Directory.CreateDirectory(ordner);
            string pfad = Path.Combine(ordner, Dateiname);
            string temp = pfad + ".tmp";
            using (var s = File.Create(temp))
                Serializer.Serialize(s, stand);
            if (File.Exists(pfad)) File.Copy(pfad, pfad + ".bak", true);
            File.Copy(temp, pfad, true);
            File.Delete(temp);
        }
    }
}
