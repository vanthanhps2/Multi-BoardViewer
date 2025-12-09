using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using Microsoft.Win32;

namespace MultiBoardViewer
{
    public partial class MainWindow : Window
    {
        // Windows API declarations for embedding external processes
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int GWL_STYLE = -16;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_CHILD = 0x40000000;
        private const int WS_BORDER = 0x00800000;
        private const int WS_CAPTION = 0x00C00000;

        // Dictionary to track processes and their containers
        private Dictionary<TabItem, ProcessInfo> _tabProcesses = new Dictionary<TabItem, ProcessInfo>();
        private int _tabCounter = 1;
        private string _boardViewerPath = "";
        private DispatcherTimer _resizeTimer;

        private class ProcessInfo
        {
            public Process Process { get; set; }
            public WindowsFormsHost Host { get; set; }
            public System.Windows.Forms.Panel Panel { get; set; }
            public string TempDirectory { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize timer for handling window resizing
            _resizeTimer = new DispatcherTimer();
            _resizeTimer.Interval = TimeSpan.FromMilliseconds(100);
            _resizeTimer.Tick += ResizeTimer_Tick;

            // Try to find BoardViewer.exe in the same directory or parent directory
            AutoDetectBoardViewerPath();
        }

        private void AutoDetectBoardViewerPath()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // Check in current directory
            string path1 = Path.Combine(appDir, "BoardViewer.exe");
            if (File.Exists(path1))
            {
                _boardViewerPath = path1;
                txtBoardViewerPath.Text = _boardViewerPath;
                return;
            }

            // Check in BoardViewer subfolder
            string path2 = Path.Combine(appDir, "BoardViewer", "BoardViewer.exe");
            if (File.Exists(path2))
            {
                _boardViewerPath = path2;
                txtBoardViewerPath.Text = _boardViewerPath;
                return;
            }

            // Check in parent directory
            string parentDir = Directory.GetParent(appDir)?.FullName;
            if (parentDir != null)
            {
                string path3 = Path.Combine(parentDir, "BoardViewer", "BoardViewer.exe");
                if (File.Exists(path3))
                {
                    _boardViewerPath = path3;
                    txtBoardViewerPath.Text = _boardViewerPath;
                    return;
                }
            }

            txtBoardViewerPath.Text = "Please browse to BoardViewer.exe location...";
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                Title = "Select BoardViewer.exe"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _boardViewerPath = openFileDialog.FileName;
                txtBoardViewerPath.Text = _boardViewerPath;
            }
        }

        private void BtnAddTab_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_boardViewerPath) || !File.Exists(_boardViewerPath))
            {
                MessageBox.Show("Please select a valid BoardViewer.exe path first.", 
                    "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CreateNewTab();
        }

        private void CreateNewTab()
        {
            try
            {
                // Create new tab
                TabItem newTab = new TabItem
                {
                    Header = $"BoardViewer {_tabCounter}"
                };

                // Create a WindowsFormsHost to embed the external process
                WindowsFormsHost host = new WindowsFormsHost();
                host.Focusable = true;
                
                System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel
                {
                    Dock = System.Windows.Forms.DockStyle.Fill
                };
                host.Child = panel;
                newTab.Content = host;

                // Add tab to control
                tabControl.Items.Add(newTab);
                tabControl.SelectedItem = newTab;

                // Start BoardViewer process directly (multi-instance is enabled in BoardViewer settings)
                Process process = new Process();
                process.StartInfo.FileName = _boardViewerPath;
                process.StartInfo.WorkingDirectory = Path.GetDirectoryName(_boardViewerPath);
                process.EnableRaisingEvents = true;
                process.Exited += (s, e) => Process_Exited(newTab);

                process.Start();
                process.WaitForInputIdle(5000);

                _tabCounter++;

                // Store process info
                _tabProcesses[newTab] = new ProcessInfo
                {
                    Process = process,
                    Host = host,
                    Panel = panel,
                    TempDirectory = null  // No temp directory needed anymore
                };

                // Embed the process window into the panel
                EmbedProcess(process, panel);

                ShowStatus($"Tab {newTab.Header} created successfully", true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating tab: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ShowStatus("Failed to create tab", false);
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            // Create destination directory
            Directory.CreateDirectory(destDir);

            // Copy all files
            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(filePath);
                string destFilePath = Path.Combine(destDir, fileName);
                File.Copy(filePath, destFilePath, true);
            }

            // Copy all subdirectories
            foreach (string dirPath in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(dirPath);
                string destDirPath = Path.Combine(destDir, dirName);
                CopyDirectory(dirPath, destDirPath);
            }
        }

        private void EmbedProcess(Process process, System.Windows.Forms.Panel panel)
        {
            try
            {
                IntPtr processHandle = process.MainWindowHandle;

                if (processHandle == IntPtr.Zero)
                {
                    // Try to wait a bit more for the window to be created
                    for (int i = 0; i < 10 && processHandle == IntPtr.Zero; i++)
                    {
                        System.Threading.Thread.Sleep(100);
                        process.Refresh();
                        processHandle = process.MainWindowHandle;
                    }
                }

                if (processHandle != IntPtr.Zero)
                {
                    // Set the process window as a child of the panel
                    SetParent(processHandle, panel.Handle);

                    // Remove window border and caption
                    int style = GetWindowLong(processHandle, GWL_STYLE);
                    style &= ~(WS_CAPTION | WS_BORDER);
                    style |= WS_CHILD;
                    SetWindowLong(processHandle, GWL_STYLE, style);

                    // Resize the window to fit the panel
                    MoveWindow(processHandle, 0, 0, panel.Width, panel.Height, true);

                    // Set focus to the embedded window
                    SetFocus(processHandle);
                    SetForegroundWindow(processHandle);

                    // Handle panel resize
                    panel.Resize += (s, e) =>
                    {
                        if (!process.HasExited && processHandle != IntPtr.Zero)
                        {
                            MoveWindow(processHandle, 0, 0, panel.Width, panel.Height, true);
                        }
                    };

                    // Handle panel click to set focus to embedded window
                    panel.Click += (s, e) =>
                    {
                        if (!process.HasExited && processHandle != IntPtr.Zero)
                        {
                            SetFocus(processHandle);
                            SetForegroundWindow(processHandle);
                        }
                    };

                    // Handle panel got focus
                    panel.GotFocus += (s, e) =>
                    {
                        if (!process.HasExited && processHandle != IntPtr.Zero)
                        {
                            SetFocus(processHandle);
                        }
                    };

                    // Handle mouse enter to give focus
                    panel.MouseEnter += (s, e) =>
                    {
                        if (!process.HasExited && processHandle != IntPtr.Zero)
                        {
                            SetFocus(processHandle);
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error embedding process: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            TabItem tabItem = button?.Tag as TabItem;

            if (tabItem != null)
            {
                CloseTabItem(tabItem);
            }
        }

        private void CloseTabItem(TabItem tabItem)
        {
            if (_tabProcesses.ContainsKey(tabItem))
            {
                ProcessInfo processInfo = _tabProcesses[tabItem];

                try
                {
                    if (processInfo.Process != null && !processInfo.Process.HasExited)
                    {
                        processInfo.Process.CloseMainWindow();
                        processInfo.Process.WaitForExit(2000);
                        
                        if (!processInfo.Process.HasExited)
                        {
                            processInfo.Process.Kill();
                        }
                        
                        processInfo.Process.Dispose();
                    }

                    // Clean up temp directory
                    if (!string.IsNullOrEmpty(processInfo.TempDirectory) && Directory.Exists(processInfo.TempDirectory))
                    {
                        try
                        {
                            Directory.Delete(processInfo.TempDirectory, true);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error deleting temp directory: {ex.Message}");
                            // Try to delete later
                            System.Threading.Tasks.Task.Run(() =>
                            {
                                System.Threading.Thread.Sleep(1000);
                                try
                                {
                                    if (Directory.Exists(processInfo.TempDirectory))
                                    {
                                        Directory.Delete(processInfo.TempDirectory, true);
                                    }
                                }
                                catch { }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error closing process: {ex.Message}");
                }

                _tabProcesses.Remove(tabItem);
            }

            tabControl.Items.Remove(tabItem);
            ShowStatus("Tab closed", true);
        }

        private void Process_Exited(TabItem tabItem)
        {
            Dispatcher.Invoke(() =>
            {
                if (_tabProcesses.ContainsKey(tabItem))
                {
                    ProcessInfo processInfo = _tabProcesses[tabItem];
                    
                    // Clean up temp directory
                    if (!string.IsNullOrEmpty(processInfo.TempDirectory) && Directory.Exists(processInfo.TempDirectory))
                    {
                        try
                        {
                            Directory.Delete(processInfo.TempDirectory, true);
                        }
                        catch { }
                    }
                    
                    _tabProcesses.Remove(tabItem);
                    tabControl.Items.Remove(tabItem);
                    ShowStatus("BoardViewer process exited", false);
                }
            });
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Trigger resize when tab is switched
            _resizeTimer.Stop();
            _resizeTimer.Start();

            // Set focus to the selected tab's BoardViewer window
            if (tabControl.SelectedItem is TabItem selectedTab && _tabProcesses.ContainsKey(selectedTab))
            {
                ProcessInfo processInfo = _tabProcesses[selectedTab];
                if (processInfo.Process != null && !processInfo.Process.HasExited)
                {
                    IntPtr handle = processInfo.Process.MainWindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        // Set focus to the embedded window
                        SetFocus(handle);
                        SetForegroundWindow(handle);
                    }
                }
            }
        }

        private void ResizeTimer_Tick(object sender, EventArgs e)
        {
            _resizeTimer.Stop();

            if (tabControl.SelectedItem is TabItem selectedTab && _tabProcesses.ContainsKey(selectedTab))
            {
                ProcessInfo processInfo = _tabProcesses[selectedTab];
                if (processInfo.Process != null && !processInfo.Process.HasExited)
                {
                    IntPtr handle = processInfo.Process.MainWindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        MoveWindow(handle, 0, 0, 
                            processInfo.Panel.Width, 
                            processInfo.Panel.Height, true);
                    }
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Close all processes when window is closing
            foreach (var kvp in _tabProcesses)
            {
                try
                {
                    if (kvp.Value.Process != null && !kvp.Value.Process.HasExited)
                    {
                        kvp.Value.Process.CloseMainWindow();
                        kvp.Value.Process.WaitForExit(1000);
                        
                        if (!kvp.Value.Process.HasExited)
                        {
                            kvp.Value.Process.Kill();
                        }
                        
                        kvp.Value.Process.Dispose();
                    }

                    // Clean up temp directory
                    if (!string.IsNullOrEmpty(kvp.Value.TempDirectory) && Directory.Exists(kvp.Value.TempDirectory))
                    {
                        try
                        {
                            Directory.Delete(kvp.Value.TempDirectory, true);
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error closing process on exit: {ex.Message}");
                }
            }

            _tabProcesses.Clear();
            
            // Clean up any remaining temp directories
            CleanupOldTempDirectories();
        }

        private void CleanupOldTempDirectories()
        {
            try
            {
                string tempRoot = Path.Combine(Path.GetTempPath(), "MultiBoardViewer");
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
            catch { }
        }

        private void ShowStatus(string message, bool isSuccess)
        {
            txtStatus.Text = message;
            txtStatus.Foreground = isSuccess ? 
                System.Windows.Media.Brushes.Green : 
                System.Windows.Media.Brushes.Red;

            // Clear status after 3 seconds
            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, e) =>
            {
                txtStatus.Text = "";
                timer.Stop();
            };
            timer.Start();
        }
    }
}
