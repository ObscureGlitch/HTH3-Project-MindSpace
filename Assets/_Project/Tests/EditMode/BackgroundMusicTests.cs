using System.Linq;
using NUnit.Framework;
using TheLastWatch.Audio;
using UnityEditor;
using UnityEngine;

namespace TheLastWatch.Tests.EditMode
{
    public sealed class BackgroundMusicTests
    {
        [Test]
        public void DayAndNightOutdoorAmbienceAreAvailableAsStreamedResources()
        {
            foreach (string resourcePath in new[]
                     {
                         "TheLastWatch/OutdoorAmbience/nature-ambience-323729",
                         "TheLastWatch/OutdoorAmbience/night-ambience-17064"
                     })
            {
                AudioClip ambience = Resources.Load<AudioClip>(resourcePath);
                Assert.That(ambience, Is.Not.Null, resourcePath);
                string path = AssetDatabase.GetAssetPath(ambience);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming), path);
                Assert.That(importer.loadInBackground, Is.True, path);
                Assert.That(importer.forceToMono, Is.False, path);
            }
        }

        [Test]
        public void OutdoorNatureAmbienceFadesAcrossTheRoomBoundary()
        {
            Assert.That(OutdoorNatureAmbience.OutdoorBlend(Vector3.zero), Is.EqualTo(0f));
            Assert.That(OutdoorNatureAmbience.OutdoorBlend(new Vector3(3.6f, 0f, 0f)),
                Is.InRange(0f, .05f));
            Assert.That(OutdoorNatureAmbience.OutdoorBlend(new Vector3(4.9f, 0f, 0f)),
                Is.EqualTo(1f));
            Assert.That(OutdoorNatureAmbience.OutdoorBlend(new Vector3(0f, 0f, -4.4f)),
                Is.EqualTo(1f));
        }

        [Test]
        public void OutdoorNatureAmbienceCrossfadesBetweenDayAndNight()
        {
            Assert.That(OutdoorNatureAmbience.DayBlend(0f), Is.EqualTo(1f));
            Assert.That(OutdoorNatureAmbience.DayBlend(.5f), Is.EqualTo(.5f));
            Assert.That(OutdoorNatureAmbience.DayBlend(1f), Is.EqualTo(0f));
        }

        [Test]
        public void AllSevenTracksAreAvailableAsStreamedResources()
        {
            AudioClip[] tracks = Resources.LoadAll<AudioClip>("TheLastWatch/BackgroundMusic");

            Assert.That(tracks, Has.Length.EqualTo(7));
            Assert.That(tracks.Select(track => track.name).Distinct().Count(), Is.EqualTo(7));
            foreach (AudioClip track in tracks)
            {
                string path = AssetDatabase.GetAssetPath(track);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming), path);
                Assert.That(importer.loadInBackground, Is.True, path);
                Assert.That(importer.forceToMono, Is.False, path);
            }
        }

        [Test]
        public void EachCyclePlaysEveryTrackExactlyOnce()
        {
            int[] tracks = Enumerable.Range(0, 7).ToArray();
            var playlist = new ShuffleBag<int>(tracks, 260926);

            int[] firstCycle = Enumerable.Range(0, tracks.Length)
                .Select(_ => playlist.Draw()).ToArray();
            int[] secondCycle = Enumerable.Range(0, tracks.Length)
                .Select(_ => playlist.Draw()).ToArray();

            CollectionAssert.AreEquivalent(tracks, firstCycle);
            CollectionAssert.AreEquivalent(tracks, secondCycle);
        }

        [Test]
        public void ReshuffleNeverRepeatsAcrossCycleBoundary()
        {
            const int trackCount = 7;
            var playlist = new ShuffleBag<int>(Enumerable.Range(0, trackCount), 260926);
            int previous = playlist.Draw();

            for (int draw = 1; draw < trackCount * 100; draw++)
            {
                int current = playlist.Draw();
                if (draw % trackCount == 0)
                {
                    Assert.That(current, Is.Not.EqualTo(previous), $"Cycle boundary at draw {draw}");
                }

                previous = current;
            }
        }
    }
}
