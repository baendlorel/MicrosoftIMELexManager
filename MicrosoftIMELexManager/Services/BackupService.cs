using System;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace MicrosoftIMELexManager.Services;

/// <summary>
/// 备份服务 - 自动创建和管理词库文件备份
/// </summary>
public static class BackupService
{
    /// <summary>
    /// 创建文件备份，使用时间戳命名
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

        // 生成时间戳文件名: ChsPinyinEUDPv1.lex.20260528_143022.bak
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"{sourceFile.Name}.{timestamp}.bak";
        var backupPath = Path.Combine(backupDir!, backupFileName);

        // 复制文件
        File.Copy(sourcePath, backupPath, overwrite: true);

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
    /// 清理旧备份，保留指定数量的最新备份
    /// </summary>
    /// <param name="directory">备份目录</param>
    /// <param name="pattern">文件匹配模式（如 "ChsPinyin*.lex.*.bak"）</param>
    /// <param name="keepCount">保留数量</param>
    public static void CleanOldBackups(string directory, string pattern, int keepCount = 5)
    {
        if (!Directory.Exists(directory))
            return;

        var backupFiles = Directory.GetFiles(directory, pattern)
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();

        // 删除超过保留数量的旧备份
        for (int i = keepCount; i < backupFiles.Count; i++)
        {
            try
            {
                backupFiles[i].Delete();
            }
            catch
            {
                // 忽略删除失败
            }
        }
    }

    /// <summary>
    /// 清理所有旧备份
    /// </summary>
    /// <param name="directory">词库目录</param>
    /// <param name="keepCount">每个文件类型保留的备份数量</param>
    public static void CleanAllOldBackups(string directory, int keepCount = 5)
    {
        if (!Directory.Exists(directory))
            return;

        CleanOldBackups(directory, "ChsPinyin*.lex.*.bak", keepCount);
        CleanOldBackups(directory, "ChsPinyin*.dat.*.bak", keepCount);
    }

    /// <summary>
    /// 检测输入法进程是否运行
    /// </summary>
    /// <returns>如果输入法进程运行则返回 true</returns>
    public static bool IsIMEProcessRunning()
    {
        var processNames = new[] { "TextInputHost", "ctfmon" };

        foreach (var processName in processNames)
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length > 0)
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
                return true;
            }
        }

        return false;
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

        var target = targetPath ?? backupPath.Replace(".bak", "");

        // 移除时间戳部分
        var match = System.Text.RegularExpressions.Regex.Match(target, @"\.\d{8}_\d{6}\.bak$");
        if (match.Success)
        {
            target = target.Substring(0, match.Index);
        }

        File.Copy(backupPath, target, overwrite: true);
    }
}
