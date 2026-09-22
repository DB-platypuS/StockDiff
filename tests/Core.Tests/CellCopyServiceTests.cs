// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: CellCopyService 单元测试，锁定复制决策的正常流程、空值边界与剪贴板异常收口。

using System.Runtime.InteropServices;
using StockDiff.Core.Copy;
using Xunit;

namespace Core.Tests;

public sealed class CellCopyServiceTests
{
    [Fact]
    // 正常流程：非空值写入剪贴板并返回「已复制」文案
    public void Copy_NonEmptyValue_WritesClipboardAndReturnsCopied()
    {
        var clipboard = new FakeClipboard();
        var service = new CellCopyService(clipboard);

        var result = service.Copy("AC04672026013030856");

        Assert.Equal(CopyStatus.Copied, result.Status);
        Assert.Equal("AC04672026013030856", result.Text);
        Assert.Equal("已复制: AC04672026013030856", result.Message);
        Assert.Equal("AC04672026013030856", clipboard.LastText);
        Assert.Equal(1, clipboard.CallCount);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("20271124")]
    [InlineData(" 保留两侧空格 ")]
    // 任意文本原样复制（不 Trim、不做数字转换），保证剪贴板与单元格显示一致
    public void Copy_AnyText_PreservesValueVerbatim(string value)
    {
        var clipboard = new FakeClipboard();
        var service = new CellCopyService(clipboard);

        var result = service.Copy(value);

        Assert.Equal(CopyStatus.Copied, result.Status);
        Assert.Equal(value, clipboard.LastText);
        Assert.Equal($"已复制: {value}", result.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    // 边界：null / 空串 → 不写剪贴板，返回 Empty 且无提示文案
    public void Copy_NullOrEmpty_DoesNotWriteClipboard(string? value)
    {
        var clipboard = new FakeClipboard();
        var service = new CellCopyService(clipboard);

        var result = service.Copy(value);

        Assert.Equal(CopyStatus.Empty, result.Status);
        Assert.Equal("", result.Text);
        Assert.Equal("", result.Message);
        Assert.Equal(0, clipboard.CallCount);
    }

    [Fact]
    // 边界：纯空白串按「非空串」口径仍复制，锁定既有行为
    public void Copy_WhitespaceOnly_IsCopiedVerbatim()
    {
        var clipboard = new FakeClipboard();
        var service = new CellCopyService(clipboard);

        var result = service.Copy(" ");

        Assert.Equal(CopyStatus.Copied, result.Status);
        Assert.Equal(" ", clipboard.LastText);
    }

    [Fact]
    // 异常：剪贴板抛错 → 收口为 Failed，不向外抛出，且携带原异常信息
    public void Copy_ClipboardThrows_ReturnsFailedWithoutThrowing()
    {
        var clipboard = new FakeClipboard { ThrowOnSet = true };
        var service = new CellCopyService(clipboard);

        var result = service.Copy("A-1");

        Assert.Equal(CopyStatus.Failed, result.Status);
        Assert.Contains("复制失败", result.Message);
        Assert.Contains("剪贴板被占用", result.Message);
    }

    [Fact]
    // 异常：构造时端口为 null → ArgumentNullException
    public void Constructor_NullClipboard_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CellCopyService(null!));
    }

    [Fact]
    // 边界：超长文本（10 万字符）原样写入，不截断、不丢失
    public void Copy_OverlongText_PreservesWholeValue()
    {
        var clipboard = new FakeClipboard();
        var service = new CellCopyService(clipboard);
        var value = new string('A', 100_000);

        var result = service.Copy(value);

        Assert.Equal(CopyStatus.Copied, result.Status);
        Assert.Equal(100_000, clipboard.LastText!.Length);
        Assert.Equal(value, clipboard.LastText);
    }

    [Theory]
    [InlineData("第一行\r\n第二行")]
    [InlineData("含\t制表符")]
    [InlineData("引号\"与'单引号")]
    [InlineData("逗号,与分号;")]
    [InlineData("emoji😀与中文")]
    // 边界：换行/制表/引号/逗号/Unicode 等特殊字符原样保留，不做转义或裁剪
    public void Copy_SpecialCharacters_PreserveVerbatim(string value)
    {
        var clipboard = new FakeClipboard();
        var service = new CellCopyService(clipboard);

        var result = service.Copy(value);

        Assert.Equal(CopyStatus.Copied, result.Status);
        Assert.Equal(value, clipboard.LastText);
    }

    [Fact]
    // 异常：真实环境下剪贴板被其它进程占用会抛 ExternalException → 同样收口为 Failed
    public void Copy_ExternalException_ReturnsFailed()
    {
        var clipboard = new FakeClipboard { ExceptionToThrow = new ExternalException("剪贴板被占用") };
        var service = new CellCopyService(clipboard);

        var result = service.Copy("A-1");

        Assert.Equal(CopyStatus.Failed, result.Status);
        Assert.Contains("复制失败", result.Message);
    }

    [Fact]
    // 并发：多线程同时复制不抛异常，每次调用都被完整记录
    public void Copy_ConcurrentCalls_AllRecordedWithoutThrow()
    {
        var clipboard = new FakeClipboard();
        var service = new CellCopyService(clipboard);

        Parallel.For(0, 200, i => service.Copy($"值{i}"));

        Assert.Equal(200, clipboard.CallCount);
    }

    // 测试替身：记录写入内容与调用次数，可模拟剪贴板占用异常；
    // 用 Interlocked/Volatile 保证并发用例的断言可靠（原实现的 ++ 存在竞态）
    private sealed class FakeClipboard : IClipboard
    {
        private int _callCount;
        private string? _lastText;

        public string? LastText => Volatile.Read(ref _lastText);
        public int CallCount => Volatile.Read(ref _callCount);
        public bool ThrowOnSet { get; init; }
        public Exception? ExceptionToThrow { get; init; }

        public void SetText(string text)
        {
            Interlocked.Increment(ref _callCount);

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            if (ThrowOnSet)
            {
                throw new InvalidOperationException("剪贴板被占用");
            }

            Volatile.Write(ref _lastText, text);
        }
    }
}
