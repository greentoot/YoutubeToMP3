using MediaToolkit;
using MediaToolkit.Model;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YoutubeToMP3
{
    public class Downloads
    {
        public async Task DownloadAsync(string url, ProgressBar progress, string fileType)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Please enter a YouTube URL.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!TryValidateYouTubeUrl(url))
            {
                MessageBox.Show("Please enter a valid YouTube URL.", "Invalid URL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string? tempAudioPath = null;

            try
            {
                var youtube = new YoutubeClient();
                var videoId = VideoId.Parse(url);
                var video = await youtube.Videos.GetAsync(videoId);

                var invalidChars = Path.GetInvalidFileNameChars();
                var safeFileName = string.Join("_", video.Title.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();

                using var saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = $"{fileType.ToUpperInvariant()} Files|*.{fileType}";
                saveFileDialog.Title = $"Save {fileType.ToUpperInvariant()} File";
                saveFileDialog.FileName = safeFileName;

                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoId);

                // Prefer audio-only streams: they are more reliable than muxed streams
                // and better match the app's MP3/OGG/AAC conversion workflow.
                var streamInfo = streamManifest.GetAudioOnlyStreams().TryGetWithHighestBitrate();

                if (streamInfo is null)
                {
                    throw new InvalidOperationException("No audio stream is available for this video.");
                }

                tempAudioPath = Path.Combine(Path.GetTempPath(), $"{videoId}.{streamInfo.Container.Name}");

                progress.Value = 0;
                progress.Maximum = 100;

                var progressHandler = new Progress<double>(value =>
                {
                    progress.Value = Math.Clamp((int)(value * 100), 0, 100);
                });

                await youtube.Videos.Streams.DownloadAsync(streamInfo, tempAudioPath, progressHandler);

                var inputFile = new MediaFile { Filename = tempAudioPath };
                var outputFile = new MediaFile { Filename = Path.ChangeExtension(saveFileDialog.FileName, fileType) };

                using var engine = new Engine();
                engine.GetMetadata(inputFile);
                engine.Convert(inputFile, outputFile);

                MessageBox.Show("Download complete!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(tempAudioPath) && File.Exists(tempAudioPath))
                {
                    File.Delete(tempAudioPath);
                }

                progress.Value = 0;
            }
        }

        public void ClearInfo(TextBox urlInput, ProgressBar progress)
        {
            urlInput.Clear();
            progress.Value = 0;
        }

        private static bool TryValidateYouTubeUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var host = uri.Host.ToLowerInvariant();
                return host.Contains("youtube.com") || host.Contains("youtu.be");
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
