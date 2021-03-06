﻿using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Astrarium.Types.Controls
{
    public class HtmlTextBlock : TextBlock
    {
        public static readonly DependencyProperty FormattedTextProperty = DependencyProperty.RegisterAttached("FormattedText", typeof(string), typeof(HtmlTextBlock), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, FormattedTextPropertyChanged));

        public static void SetFormattedText(DependencyObject textBlock, string value)
        {
            textBlock.SetValue(FormattedTextProperty, value);
        }

        public static string GetFormattedText(DependencyObject textBlock)
        {
            return (string)textBlock.GetValue(FormattedTextProperty);
        }

        static void FormattedTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is TextBlock textBlock)) return;

            textBlock.Inlines.Clear();
            string value = (string)e.NewValue;

            if (!string.IsNullOrEmpty(value))
            {
                int lastIndex = 0;

                MatchCollection m1 = Regex.Matches(value, @"(<a.*?>.*?</a>)", RegexOptions.Singleline);

                foreach (Match m2 in m1)
                {
                    var tb = new TextBlock(new Run(value.Substring(lastIndex, m2.Index - lastIndex))) { FontSize = textBlock.FontSize };
                    textBlock.Inlines.Add(tb);

                    string fullLinkWithTags = m2.Groups[1].Value;

                    Match m3 = Regex.Match(fullLinkWithTags, @"href=[\""'](.*?)[\""']", RegexOptions.Singleline);
                    if (m3.Success)
                    {
                        Hyperlink link = new Hyperlink();
                        link.NavigateUri = new Uri(m3.Groups[1].Value);
                        link.RequestNavigate += HyperLink_RequestNavigate;
                        string innerText = Regex.Replace(fullLinkWithTags, @"\s*<.*?>\s*", "", RegexOptions.Singleline);
                        link.Inlines.Add(innerText);
                        lastIndex = m2.Index + m2.Length;
                        textBlock.Inlines.Add(link);
                    }
                }

                if (lastIndex <= value.Length - 1)
                {
                    var tb = new TextBlock(new Run(value.Substring(lastIndex))) { FontSize = textBlock.FontSize };
                    textBlock.Inlines.Add(tb);
                }
            }
            else
            {
                var tb = new TextBlock(new Run(value)) { FontSize = textBlock.FontSize };

                textBlock.Inlines.Add(tb);
            }
        }

        static void HyperLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(e.Uri.ToString());
            }
            catch { }
        }
    }
}
