using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MicrosoftIMELexManager.Services;

/// <summary>
/// 备份服务 - 自动创建和管理词库文件备份
/// </summary>
public static class BackupService
{
    /// <summary>
    /// 创建文件备份，命名为 原文件名+源文件后缀+.bak
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <param name="backupDirectory">备份目录（默认与源文件同目录）</param>
    /// <returns>备份文件路径</returns>
    public static string CreateBackup(string sourcePath, string? backupDirectory = null)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("源文件不存在", sourcePath);

        var sourceFile = new FileInfo(sourcePath);
        var backupDir = backupDirectory ?? sourceFile.DirectoryName;

        if (!Directory.Exists(backupDir))
            Directory.CreateDirectory(backupDir);

        var backupFileName = $"{sourceFile.Name}.bak";
        var backupPath = Path.Combine(backupDir!, backupFileName);

        if (!File.Exists(backupPath))
        {
            File.Copy(sourcePath, backupPath, overwrite: false);
        }

        return backupPath;
    }

    /// <summary>
    /// 在写入前创建备份
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <returns>备份文件路径，如果源文件不存在则返回 null</returns>
    public static string? CreateBackupBeforeWrite(string sourcePath)
    {
        if (!File.Exists(sourcePath))
            return null;

        try
        {
            return CreateBackup(sourcePath);
        }
        catch
        {
            // 备份失败不应阻止写入操作
            return null;
        }
    }

    /// <summary>
    /// 检测输入法进程是否运行
    /// </summary>
    /// <returns>如果输入法进程运行则返回 true</returns>
    public static bool IsIMEProcessRunning()
    {
        return new[] { "TextInputHost", "ctfmon" }.Any(name =>
        {
            var processes = Process.GetProcessesByName(name);
            foreach (var p in processes) p.Dispose();
            return processes.Length > 0;
        });
    }

    public static (bool Success, string Message) RefreshIME()
    {
        var stoppedProcesses = new System.Collections.Generic.List<string>();

        try
        {
            foreach (var processName in new[] { "TextInputHost", "ctfmon" })
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        var displayName = $"{process.ProcessName}({process.Id})";
                        process.Kill(entireProcessTree: false);
                        process.WaitForExit(3000);
                        stoppedProcesses.Add(displayName);
                    }
                    catch
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "ctfmon.exe",
                UseShellExecute = true
            };

            using var restarted = Process.Start(startInfo);
            var stoppedSummary = stoppedProcesses.Count > 0
                ? $"已停止: {string.Join(", ", stoppedProcesses.Distinct())}。"
                : "未检测到可终止的输入法进程。";

            return (true, $"{stoppedSummary} 已重新启动 ctfmon.exe，请切换一次输入法后重试。");
        }
        catch (Exception ex)
        {
            var stoppedSummary = stoppedProcesses.Count > 0
                ? $"已停止部分进程: {string.Join(", ", stoppedProcesses.Distinct())}。"
                : "未能停止输入法相关进程。";

            return (false, $"{stoppedSummary} 刷新输入法失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 获取备份文件列表
    /// </summary>
    /// <param name="directory">备份目录</param>
    /// <param name="pattern">文件匹配模式</param>
    public static string[] GetBackupFiles(string directory, string pattern = "*.bak")
    {
        if (!Directory.Exists(directory))
            return Array.Empty<string>();

        return Directory.GetFiles(directory, pattern);
    }

    /// <summary>
    /// 从备份恢复
    /// </summary>
    /// <param name="backupPath">备份文件路径</param>
    /// <param name="targetPath">目标文件路径</param>
    public static void RestoreFromBackup(string backupPath, string? targetPath = null)
    {
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("备份文件不存在", backupPath);

        var target = targetPath ?? GetRestoreTargetPath(backupPath);

        File.Copy(backupPath, target, overwrite: true);
    }

    /// <summary>
    /// 获取备份文件对应的恢复目标路径
    /// </summary>
    public static string GetRestoreTargetPath(string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
            throw new ArgumentException("备份文件路径不能为空", nameof(backupPath));

        if (!backupPath.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("仅支持 .bak 备份文件", nameof(backupPath));

        return backupPath[..^4];
    }
}
