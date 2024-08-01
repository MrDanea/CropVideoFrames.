using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Size = System.Drawing.Size;

namespace MoCapTest
{
    public partial class MainWindow : Window
    {
        private string[] imageFiles;
        private string videoFilePath;
        private int fps;

        public MainWindow()
        {
            InitializeComponent();
            fps = 24; // Mặc định là 24fps
            FpsTextBox.Text = fps.ToString();
        }

        private void LoadVideoButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Video files (*.mp4;*.avi)|*.mp4;*.avi"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                videoFilePath = openFileDialog.FileName;
                ExtractFramesButton.IsEnabled = true;
                MessageBox.Show("Video loaded successfully.");
            }
        }

        private void LoadImagesButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };

            if (dialog.ShowDialog() == true)
            {
                imageFiles = dialog.FileNames;
                Array.Sort(imageFiles);
                CreateVideoButton.IsEnabled = true;
                MessageBox.Show($"Loaded {imageFiles.Length} images.");
            }
        }

        private async void ExtractFramesButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(videoFilePath))
            {
                MessageBox.Show("Please load a video first.");
                return;
            }

            if (!int.TryParse(FpsTextBox.Text, out fps) || fps <= 0)
            {
                MessageBox.Show("Please enter a valid positive number for FPS.");
                return;
            }

            ExtractFramesButton.IsEnabled = false;
            ExtractProgressBar.Value = 0;

            var folderDialog = new OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Folder Selection."
            };

            if (folderDialog.ShowDialog() == true)
            {
                string selectedPath = Path.GetDirectoryName(folderDialog.FileName);
                await ExtractFramesAsync(selectedPath);
            }

            ExtractFramesButton.IsEnabled = true;
        }

        private async Task ExtractFramesAsync(string outputFolder)
        {
            using (VideoCapture capture = new VideoCapture(videoFilePath))
            {
                int frameCount = (int)capture.Get(CapProp.FrameCount);
                int originalFps = (int)capture.Get(CapProp.Fps);
                double frameInterval = originalFps / (double)fps;
                int frameNumber = 0;

                for (double i = 0; i < frameCount; i += frameInterval)
                {
                    capture.Set(CapProp.PosFrames, (int)i);
                    Mat frame = new Mat();
                    capture.Read(frame);

                    if (!frame.IsEmpty)
                    {
                        string frameFileName = Path.Combine(outputFolder, $"frame_{frameNumber:D4}.jpg");
                        frame.Save(frameFileName);
                        frameNumber++;

                        await Dispatcher.InvokeAsync(() =>
                        {
                            ExtractProgressBar.Value = i * 100.0 / frameCount;
                            FrameImage.Source = ConvertMatToBitmapSource(frame);
                        });
                    }

                    await Task.Delay(1); // Cho phép UI cập nhật
                }
            }

            MessageBox.Show("Frames extracted successfully!");
        }

        private async void CreateVideoButton_Click(object sender, RoutedEventArgs e)
        {
            if (imageFiles == null || imageFiles.Length == 0)
            {
                MessageBox.Show("Please load images first.");
                return;
            }

            if (!int.TryParse(FpsTextBox.Text, out fps) || fps <= 0)
            {
                MessageBox.Show("Please enter a valid positive number for FPS.");
                return;
            }

            CreateVideoButton.IsEnabled = false;
            CreateVideoProgressBar.Value = 0;

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "MP4 files (*.mp4)|*.mp4",
                DefaultExt = "mp4"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                await CreateVideoAsync(saveFileDialog.FileName);
            }

            CreateVideoButton.IsEnabled = true;
        }

        private async Task CreateVideoAsync(string outputPath)
        {
            int width = 0, height = 0;

            using (var firstImage = new Mat(imageFiles[0], ImreadModes.Color))
            {
                width = firstImage.Width;
                height = firstImage.Height;
            }

            using (var writer = new VideoWriter(outputPath, VideoWriter.Fourcc('H', '2', '6', '4'), fps, new Size(width, height), true))
            {
                for (int i = 0; i < imageFiles.Length; i++)
                {
                    using (var image = new Mat(imageFiles[i], ImreadModes.Color))
                    {
                        writer.Write(image);
                    }

                    await Dispatcher.InvokeAsync(() =>
                    {
                        CreateVideoProgressBar.Value = (i + 1) * 100.0 / imageFiles.Length;
                        FrameImage.Source = new BitmapImage(new Uri(imageFiles[i]));
                    });

                    await Task.Delay(1); // Cho phép UI cập nhật
                }
            }

            MessageBox.Show("Video created successfully!");
        }

        private BitmapSource ConvertMatToBitmapSource(Mat image)
        {
            Image<Bgr, byte> img = image.ToImage<Bgr, byte>();
            byte[] data = new byte[img.Width * img.Height * 3];
            Buffer.BlockCopy(img.ManagedArray, 0, data, 0, data.Length);

            int stride = img.Width * 3; // 3 bytes per pixel (BGR)

            return BitmapSource.Create(
                img.Width,
                img.Height,
                96, // DpiX
                96, // DpiY
                PixelFormats.Bgr24,
                null,
                data,
                stride);
        }
    }
}