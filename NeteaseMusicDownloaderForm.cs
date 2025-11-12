using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdbFileUploader
{
    public partial class NeteaseMusicDownloaderForm : Form
    {
        // 添加API相关字段
        private const string ApiBaseUrl = "https://wyapi.toubiec.cn";
        private readonly HttpClient _httpClient;
        private List<SongInfo> _currentSongs = new List<SongInfo>();
        private string _tempDownloadDir;

        // SongInfo类用于存储歌曲信息
        private class SongInfo
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Artists { get; set; }
            public string Album { get; set; }
            public string Url { get; set; }
            public string PicUrl { get; set; }
            public string Quality { get; set; }
            public string Size { get; set; }
            public string FilePath { get; set; } // 本地文件路径
        }

        public NeteaseMusicDownloaderForm()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("Origin", "https://wyapi.toubiec.cn");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://wyapi.toubiec.cn/");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            this.Icon = new Icon("icon.ico");
            // 创建临时下载目录
            _tempDownloadDir = Path.Combine(Path.GetTempPath(), "NeteaseMusicTemp");
            if (!Directory.Exists(_tempDownloadDir))
            {
                Directory.CreateDirectory(_tempDownloadDir);
            }

            InitializeUI();
        }
        private void TxtUrl_GotFocus(object sender, EventArgs e)
        {
            TextBox tb = (TextBox)sender;
            if (tb.Text == "请输入歌曲ID（如：435278010）")
            {
                tb.Text = "";
                tb.ForeColor = Color.Black;
            }
        }
        private void TxtUrl_LostFocus(object sender, EventArgs e)
        {
            TextBox tb = (TextBox)sender;
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                tb.Text = "请输入歌曲ID（如：435278010）";
                tb.ForeColor = Color.Gray;
            }
        }
        private void InitializeUI()
        {
            this.Text = "网易云音乐下载";
            this.Size = new Size(600, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // URL输入区域
            Label lblUrl = new Label
            {
                Text = "歌曲ID:",
                Location = new Point(20, 20),
                Size = new Size(80, 20)
            };

            TextBox txtUrl = new TextBox
            {
                Name = "txtUrl",
                Location = new Point(100, 20),
                Size = new Size(300, 20),
                ForeColor = Color.Gray
            };
            txtUrl.GotFocus += TxtUrl_GotFocus;
            txtUrl.LostFocus += TxtUrl_LostFocus;
            txtUrl.Text = "请输入歌曲ID（如：435278010）";

            // 音质选择
            Label lblQuality = new Label
            {
                Text = "音质:",
                Location = new Point(20, 50),
                Size = new Size(50, 20)
            };

            ComboBox cboQuality = new ComboBox
            {
                Name = "cboQuality",
                Location = new Point(100, 50),
                Size = new Size(120, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboQuality.Items.AddRange(new object[] { "标准", "极高", "无损", "Hi-Res", "高清环绕" });
            cboQuality.SelectedIndex = 3; // 默认选择无损

            // 解析按钮
            Button btnParse = new Button
            {
                Text = "解析",
                Location = new Point(410, 20),
                Size = new Size(80, 23)
            };
            btnParse.Click += async (s, e) => await ParseUrlAsync();

            // 歌曲列表
            Label lblSongs = new Label
            {
                Text = "歌曲列表:",
                Location = new Point(20, 80),
                Size = new Size(80, 20)
            };

            ListBox lstSongs = new ListBox
            {
                Name = "lstSongs",
                Location = new Point(20, 100),
                Size = new Size(510, 100),
                SelectionMode = SelectionMode.MultiExtended
            };

            // 信息显示区域
            TextBox txtInfo = new TextBox
            {
                Name = "txtInfo",
                Location = new Point(20, 210),
                Size = new Size(510, 80),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };

            // 进度区域
            ProgressBar progressBar = new ProgressBar
            {
                Name = "progressBar",
                Location = new Point(20, 300),
                Size = new Size(400, 23),
                Visible = false,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            Label lblStatus = new Label
            {
                Name = "lblStatus",
                Location = new Point(430, 300),
                Size = new Size(150, 20),
                Text = "就绪"
            };

            // 操作按钮 - 位置调整
            Button btnDownload = new Button
            {
                Name = "btnDownload",
                Text = "下载选中歌曲",
                Location = new Point(410, 210), // 确保这个位置不会被其他控件遮挡
                Size = new Size(120, 28)
            };
            btnDownload.Click += async (s, e) => await DownloadSelectedSongsAsync();

            Button btnCancel = new Button
            {
                Name = "btnCancel",
                Text = "取消",
                Location = new Point(410, 250),
                Size = new Size(120, 28),
                Visible = false
            };
            btnCancel.Click += (s, e) => CancelDownload();

            // 添加到窗体
            this.Controls.Add(lblUrl);
            this.Controls.Add(txtUrl);
            this.Controls.Add(lblQuality);
            this.Controls.Add(cboQuality);
            this.Controls.Add(btnParse);
            this.Controls.Add(lblSongs);
            this.Controls.Add(lstSongs);
            this.Controls.Add(txtInfo);
            this.Controls.Add(progressBar);
            this.Controls.Add(lblStatus);
            this.Controls.Add(btnDownload);
            this.Controls.Add(btnCancel);
            btnDownload.BringToFront();
            btnCancel.BringToFront();
        }
        private async Task ParseUrlAsync()
        {
            var txtUrl = this.Controls["txtUrl"] as TextBox;
            var cboQuality = this.Controls["cboQuality"] as ComboBox;
            var lstSongs = this.Controls["lstSongs"] as ListBox;
            var txtInfo = this.Controls["txtInfo"] as TextBox;
            var lblStatus = this.Controls["lblStatus"] as Label;
            var progressBar = this.Controls["progressBar"] as ProgressBar;

            if (string.IsNullOrWhiteSpace(txtUrl.Text))
            {
                MessageBox.Show("请输入歌曲ID", "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                lblStatus.Text = "正在解析...";
                progressBar.Visible = true;
                progressBar.Value = 10;

                string songId = txtUrl.Text.Trim();
                _currentSongs.Clear();
                lstSongs.Items.Clear();
                txtInfo.Text = "解析中...";

                // 获取歌曲详情
                var songDetail = await GetSongDetailAsync(songId);
                if (songDetail == null)
                {
                    lblStatus.Text = "解析失败";
                    txtInfo.Text = "错误：无法获取歌曲信息，请检查歌曲ID是否正确";
                    return;
                }

                progressBar.Value = 50;
                lblStatus.Text = "正在获取下载信息...";

                // 创建歌曲信息对象
                var song = new SongInfo
                {
                    Id = songDetail["id"].ToString(),
                    Name = songDetail["name"].ToString(),
                    Artists = songDetail["singer"].ToString(),
                    Album = songDetail["album"].ToString(),
                    PicUrl = songDetail["picimg"].ToString()
                };
                _currentSongs.Add(song);

                // 显示歌曲信息
                txtInfo.Text = $"歌曲: {song.Name}\n艺术家: {song.Artists}\n专辑: {song.Album}\n时长: {songDetail["duration"]}";
                lstSongs.Items.Add($"{song.Artists} - {song.Name} ({song.Album})");

                // 获取下载URL
                string quality = GetQualityValue(cboQuality.SelectedIndex);
                var urlData = await GetSongUrlAsync(song.Id, quality);
                if (urlData != null && !string.IsNullOrEmpty(urlData["url"].ToString()))
                {
                    song.Url = urlData["url"].ToString();
                    song.Quality = urlData["level"].ToString();
                    song.Size = $"{urlData["size"].Value<long>() / 1024 / 1024:F1} MB";
                    txtInfo.Text += $"\n音质: {song.Quality}\n大小: {song.Size}";
                }
                else
                {
                    txtInfo.Text += $"\n音质: {GetQualityName(cboQuality.SelectedIndex)} (不可用)";
                }

                progressBar.Value = 100;
                lblStatus.Text = "解析完成";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "解析失败";
                txtInfo.Text = $"错误: {ex.Message}";
                MessageBox.Show($"解析失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private string GetQualityValue(int selectedIndex)
        {
            switch (selectedIndex)
            {
                case 0: return "standard"; // 标准
                case 1: return "exhigh"; // 高品
                case 2: return "lossless"; // 极高
                case 3: return "hires"; // 无损
                case 4: return "jyeffect"; // Hi-Res
                default: return "lossless";
            }
        }

        private string GetQualityName(int selectedIndex)
        {
            switch (selectedIndex)
            {
                case 0: return "标准";
                case 1: return "高品";
                case 2: return "极高";
                case 3: return "无损";
                case 4: return "Hi-Res";
                default: return "无损";
            }
        }

        private async Task<JObject> GetSongDetailAsync(string songId)
        {
            try
            {
                // 准备请求内容
                var requestData = new { id = songId };
                var requestJson = JsonConvert.SerializeObject(requestData);

                

                var content = new StringContent(
                    requestJson,
                    Encoding.UTF8,
                    "application/json"
                );

                

                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/api/music/detail", content);

                // 获取响应内容
                var responseContent = await response.Content.ReadAsStringAsync();

                


                // 检查响应是否成功
                if (response.IsSuccessStatusCode)
                {
                    var json = JsonConvert.DeserializeObject<JObject>(responseContent);

                    // 检查API返回的code
                    int code = json["code"].Value<int>();
                    string msg = json["msg"].ToString();

                    

                    if (code == 200)
                    {
                        return json["data"] as JObject;
                    }
                }

                return null;
            }
            catch (TaskCanceledException)
            {
                // 请求超时
                Invoke(new Action(() => {
                    var lblStatus = this.Controls["lblStatus"] as Label;
                    lblStatus.Text = "API请求超时";
                    MessageBox.Show("API请求超时，请检查网络连接或尝试稍后重试",
                                   "请求超时", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }));
                return null;
            }
            catch (Exception ex)
            {
                // 捕获其他异常
                Debug.WriteLine($"[API DEBUG] 异常详情: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[API DEBUG] 异常堆栈: {ex.StackTrace}");

                Invoke(new Action(() => {
                    var lblStatus = this.Controls["lblStatus"] as Label;
                    lblStatus.Text = $"API调用出错: {ex.Message}";
                    MessageBox.Show($"API调用出错:\n{ex.Message}\n\n" +
                                   $"详细信息:\n{ex.GetType().Name}\n\n" +
                                   $"请检查:\n1. 网络连接\n2. API地址是否正确\n3. 防火墙设置",
                                   "API错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
                return null;
            }
        }
        private async Task<JObject> GetSongUrlAsync(string songId, string quality)
        {
            try
            {
                var content = new StringContent(
                    JsonConvert.SerializeObject(new
                    {
                        id = songId,
                        level = quality
                    }),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/api/music/url", content);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var json = JsonConvert.DeserializeObject<JObject>(result);
                    if (json["code"].Value<int>() == 200 && json["data"].HasValues)
                    {
                        return json["data"][0] as JObject; // 取第一个结果
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task DownloadSelectedSongsAsync()
        {
            var lstSongs = this.Controls["lstSongs"] as ListBox;
            var progressBar = this.Controls["progressBar"] as ProgressBar;
            var lblStatus = this.Controls["lblStatus"] as Label;
            var btnCancel = this.Controls["btnCancel"] as Button;
            var btnDownload = this.Controls.Find("btnDownload", true).FirstOrDefault() as Button;
            var cboQuality = this.Controls["cboQuality"] as ComboBox;

            if (lstSongs.SelectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要下载的歌曲", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                lblStatus.Text = "准备下载...";
                progressBar.Visible = true;
                progressBar.Value = 0;
                btnCancel.Visible = true;
                btnDownload.Enabled = false;

                List<string> downloadedFiles = new List<string>();
                int totalSongs = lstSongs.SelectedItems.Count;
                int currentSong = 0;

                foreach (var item in lstSongs.SelectedItems)
                {
                    currentSong++;
                    int songIndex = lstSongs.Items.IndexOf(item);
                    if (songIndex >= 0 && songIndex < _currentSongs.Count)
                    {
                        var song = _currentSongs[songIndex];
                        lblStatus.Text = $"正在下载 ({currentSong}/{totalSongs})...";

                        // 如果还没有解析出下载URL，再调用一次API
                        if (string.IsNullOrEmpty(song.Url))
                        {
                            string quality = GetQualityValue(cboQuality.SelectedIndex);
                            var urlData = await GetSongUrlAsync(song.Id, quality);
                            if (urlData != null && !string.IsNullOrEmpty(urlData["url"].ToString()))
                            {
                                song.Url = urlData["url"].ToString();
                                song.Quality = urlData["level"].ToString();
                                song.Size = $"{urlData["size"].Value<long>() / 1024 / 1024:F1} MB";
                            }
                        }

                        if (!string.IsNullOrEmpty(song.Url))
                        {
                            // 使用歌曲名-歌手作为文件名，并替换非法字符
                            bool isFlac = song.Url.IndexOf("flac", StringComparison.OrdinalIgnoreCase) >= 0;
                            string fileName;
                            if (isFlac) { fileName = $"{song.Name}-{song.Artists}.flac"; }
                            else { fileName = $"{song.Name}-{song.Artists}.mp3"; }
                            fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
                            string filePath = Path.Combine(_tempDownloadDir, fileName);
                            // 下载文件
                            using (var response = await _httpClient.GetAsync(song.Url, HttpCompletionOption.ResponseHeadersRead))
                            {
                                response.EnsureSuccessStatusCode();
                                long totalBytes = response.Content.Headers.ContentLength ?? -1;

                                using (var contentStream = await response.Content.ReadAsStreamAsync())
                                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    var buffer = new byte[8192];
                                    long totalBytesRead = 0;
                                    int bytesRead;
                                    int lastProgressValue = 0;

                                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                    {
                                        await fs.WriteAsync(buffer, 0, bytesRead);
                                        totalBytesRead += bytesRead;

                                        // 计算总进度：已下载文件数 + 当前文件进度
                                        double fileProgress = totalBytes != -1 ? (double)totalBytesRead / totalBytes : 0;
                                        int overallProgress = (int)((currentSong - 1 + fileProgress) / totalSongs * 100);

                                        // 避免频繁更新UI
                                        if (overallProgress > lastProgressValue)
                                        {
                                            progressBar.Value = overallProgress;
                                            lblStatus.Text = $"正在下载 ({currentSong}/{totalSongs}) - {overallProgress}%";
                                            Application.DoEvents(); // 允许UI更新
                                            lastProgressValue = overallProgress;
                                        }
                                    }
                                }
                            }

                            song.FilePath = filePath;
                            downloadedFiles.Add(filePath);
                        }
                        else
                        {
                            lblStatus.Text = $"跳过 {song.Name} (URL不可用)";
                        }
                    }
                }

                lblStatus.Text = "下载完成";
                progressBar.Value = 100;

                // 通知主窗体
                OnFilesDownloaded(downloadedFiles);

                MessageBox.Show($"成功下载 {downloadedFiles.Count} 首歌曲到临时目录",
                               "下载完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "下载失败";
                MessageBox.Show($"下载失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnDownload.Enabled = true;
                btnCancel.Visible = false;
            }
        }

        private void CancelDownload()
        {
            var progressBar = this.Controls["progressBar"] as ProgressBar;
            var lblStatus = this.Controls["lblStatus"] as Label;
            var btnCancel = this.Controls["btnCancel"] as Button;
            var btnDownload = this.Controls.Find("btnDownload", true).FirstOrDefault() as Button;

            progressBar.Visible = false;
            lblStatus.Text = "已取消下载";
            btnCancel.Visible = false;
            btnDownload.Enabled = true;
        }

        public event Action<List<string>> FilesDownloaded;

        protected virtual void OnFilesDownloaded(List<string> files)
        {
            FilesDownloaded?.Invoke(files);
        }

        
    }
}