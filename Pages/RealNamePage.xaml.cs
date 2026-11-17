using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CS2Box.Services;

namespace CS2Box.Pages;

public partial class RealNamePage : Page
{
    string _token = "";
    string _name = "";
    string _id = "";
    string _phone = "";
    string _capTicket = "";
    string _secCode = "";
    bool _smsReady;      // 已经拿到短信码了，改身份时不用再发一次

    public RealNamePage()
    {
        InitializeComponent();
        ShowStep(1);
    }

    // ---------------- 步骤切换 ----------------

    void ShowStep(int n)
    {
        PanelLink.Visibility = n == 1 ? Visibility.Visible : Visibility.Collapsed;
        PanelId.Visibility = n == 2 ? Visibility.Visible : Visibility.Collapsed;
        PanelPhone.Visibility = n == 3 ? Visibility.Visible : Visibility.Collapsed;
        PanelCode.Visibility = n == 4 ? Visibility.Visible : Visibility.Collapsed;
        StepText.Text = $"第 {n} / 4 步";

        switch (n)
        {
            case 1: LinkBox.Focus(); break;
            case 2: NameBox.Focus(); break;
            case 3: PhoneBox.Focus(); break;
            case 4: CodeBox.Focus(); break;
        }
    }

    void Say(string msg, bool error = false)
    {
        Status.Text = msg;
        Status.Foreground = error
            ? new SolidColorBrush(Color.FromRgb(196, 43, 28))
            : (Brush)FindResource("TextFillColorSecondaryBrush");
    }

    static void Busy(bool on, params Control[] controls)
    {
        foreach (var c in controls) c.IsEnabled = !on;
    }

    // ---------------- 第 1 步 ----------------

    async void Link_Click(object sender, RoutedEventArgs e)
    {
        var token = RealName.ExtractToken(LinkBox.Text);
        if (token.Length == 0)
        {
            Say("链接里没找到 token，检查一下是不是复制全了", true);
            return;
        }

        _token = token;
        Busy(true, BtnLink, BtnId, BtnPhone, BtnSubmit);
        Say("正在检查链接…");

        await Toast.Try(async () =>
        {
            var (ok, code, desc) = await RealName.CheckTokenAsync(token);

            // 刚拿到的链接要先走一遍协议页才算激活
            if (!ok) await RealName.WarmAsync(token);
            if (!ok) (ok, code, desc) = await RealName.CheckTokenAsync(token);

            if (!ok)
            {
                Say($"这条链接用不了：{RealNameResult.Describe(int.TryParse(code, out var c) ? c : -1, desc)}", true);
                return;
            }

            Say($"链接有效（{token[..8]}…{token[^4..]}）");
            ShowStep(2);
        });

        Busy(false, BtnLink, BtnId, BtnPhone, BtnSubmit);
    }

    // ---------------- 第 2 步 ----------------

    void Id_TextChanged(object sender, TextChangedEventArgs e)
    {
        var id = IdBox.Text.Trim().ToUpperInvariant();
        if (id.Length != 18 || RealName.CheckId(id) != null)
        {
            IdInfoText.Text = "";
            return;
        }
        var (birth, prov, sex, age) = RealName.IdInfo(id);
        IdInfoText.Text = $"{birth:yyyy-MM-dd} · {prov} · {sex} · {age} 岁";
    }

    void Id_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        var raw = IdBox.Text.Trim();

        // 「姓名----身份证」整行粘进来也认
        if (name.Contains("----") && string.IsNullOrWhiteSpace(raw))
        {
            var parts = name.Split("----", 2);
            name = parts[0].Trim();
            raw = parts[1].Trim();
            NameBox.Text = name;
            IdBox.Text = raw;
        }

        if (RealName.CheckName(name) is { } e1) { Say(e1, true); return; }
        if (RealName.CheckId(raw) is { } e2) { Say(e2, true); return; }

        _name = name;
        _id = raw.ToUpperInvariant();

        if (_smsReady)
        {
            Say("身份改了，验证码还能用，直接提交");
            ShowStep(4);
            return;
        }

        ShowStep(3);
        Say("");
    }

    // ---------------- 第 3 步 ----------------

    async void Phone_Click(object sender, RoutedEventArgs e)
    {
        var phone = PhoneBox.Text.Trim();
        if (RealName.CheckPhone(phone) is { } err)
        {
            Say(err, true);
            return;
        }

        _phone = phone;
        Busy(true, BtnLink, BtnId, BtnPhone, BtnSubmit);

        await Toast.Try(async () =>
        {
            Say("正在过滑块…");
            (_capTicket, _secCode, var dist) = await RealName.SolveCaptchaAsync(6, m => Say(m));

            Say("正在发短信…");
            var r = await RealName.SendSmsAsync(phone, _capTicket, _secCode);
            if (!r.Ok)
            {
                Say($"发码失败：{r.Text}", true);
                return;
            }

            _smsReady = true;
            CodeHint.Text = $"已发到 {phone[..3]}****{phone[7..]}，60 秒内有效。";
            Say($"发码成功（滑块距离 {dist}）");
            ShowStep(4);
        });

        Busy(false, BtnLink, BtnId, BtnPhone, BtnSubmit);
    }

    // ---------------- 第 4 步 ----------------

    async void Submit_Click(object sender, RoutedEventArgs e)
    {
        var sms = CodeBox.Text.Trim();
        if (sms.Length != 6 || !sms.All(char.IsDigit))
        {
            Say("验证码是 6 位数字", true);
            return;
        }

        Busy(true, BtnLink, BtnId, BtnPhone, BtnSubmit);
        Say("正在提交…");

        await Toast.Try(async () =>
        {
            var r = await RealName.SubmitAsync(_token, _name, _id, _phone, sms);

            if (r.Ok)
            {
                Say("实名信息提交成功，正在收尾…");
                var done = await RealName.CompleteAgreementAsync(_token);
                Say(done
                    ? "认证完成。回浏览器重新登录一下就生效了。"
                    : "实名过了，但收尾那步没成功，去网页上点一下「完成」。");
                BtnSubmit.Content = "已完成";
                BtnSubmit.IsEnabled = false;
                return;
            }

            // 身份对不上：退回去改姓名/身份证，短信码不用重发
            if (r.Code is 1044 or 1001)
            {
                Say($"{r.Text}　—　改一下姓名或身份证，验证码不用重发", true);
                _smsReady = true;
                ShowStep(2);
                return;
            }

            // 短信码不对：留在这一步重填
            if (r.Code is 1002 or 1006)
            {
                Say($"{r.Text}　—　重新填一次验证码", true);
                CodeBox.Clear();
                CodeBox.Focus();
                return;
            }

            Say(r.Text, true);
        });

        Busy(false, BtnLink, BtnId, BtnPhone, BtnSubmit);
        if (BtnSubmit.Content as string == "已完成") BtnSubmit.IsEnabled = false;
    }
}
