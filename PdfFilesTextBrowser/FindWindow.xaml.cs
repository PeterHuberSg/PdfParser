using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;


namespace PdfFilesTextBrowser {

  /// <summary>
  /// Interaction logic for FindWindow.xaml
  /// </summary>
  public partial class FindWindow: Window {

    readonly TabControl? pagesTabControl;
    readonly TextBox? streamTextBox;
    readonly Action callOwnerWhenClosing;
    readonly TextBox? searchedTextBox;
    readonly TextViewer? searchedTextViewer;
    readonly RichTextBox? searchedRichTextBox;


    public FindWindow(Window owner, TabControl? pagesTabControl, TextBox? streamTextBox, Action callOwnerWhenClosing) {
      Owner = owner;
      this.pagesTabControl = pagesTabControl;
      this.streamTextBox = streamTextBox;
      this.callOwnerWhenClosing = callOwnerWhenClosing;
      Left  = Owner.Left + Owner.ActualWidth*.35;
      Top   = Owner.Top + Owner.ActualHeight*0.1;

      InitializeComponent();

      if (pagesTabControl is null) {
        //called from PdfStreamWindow
        searchedTextBox = streamTextBox!;
        TextTextbox.Text = searchedTextBox.SelectedText;
        AllPagesLabel.Visibility = Visibility.Collapsed;
        AllPagesCheckBox.Visibility = Visibility.Collapsed;

      } else {
        //called from MainWindow
        var selectedTabItem = ((TabItem)pagesTabControl.SelectedItem).Content;
        if (selectedTabItem is TextBox isTextBox) {
          searchedTextBox = isTextBox;
          TextTextbox.Text = searchedTextBox.SelectedText;
        } else if (selectedTabItem is TextViewer isTextViewer) {
          searchedTextViewer = isTextViewer;
          TextTextbox.Text = isTextViewer.TextViewerSelection.Selection?.GetAllCharacters().ToString();
        } else if (selectedTabItem is RichTextBox isRichTextBox) {
          searchedRichTextBox = isRichTextBox;
          TextTextbox.Text = searchedRichTextBox.Selection.Text;
        } else {
          System.Diagnostics.Debugger.Break();
          throw new NotSupportedException();
        }

      }
      TextTextbox.Select(TextTextbox.Text.Length, 0);
      NextButton.IsEnabled = true;
      PreviousButton.IsEnabled = true;

      PreviewKeyUp += findWindow_PreviewKeyUp;
      Loaded += findWindow_Loaded;
      TextTextbox.TextChanged += textTextbox_TextChanged;
      NextButton.Click += nextButton_Click;
      PreviousButton.Click += previousButton_Click;
      Closed += findWindow_Closed;
    }


    private void findWindow_PreviewKeyUp(object sender, KeyEventArgs e) {
      if (e.Key==Key.Return) {
        if (e.OriginalSource!=NextButton && e.OriginalSource!=PreviousButton) {
          FindNext();
          e.Handled = true;
        }
      } else if (e.Key==Key.Escape) {
        Close();
      }
    }


    //private void locationTextbox_PreviewTextInput(object sender, TextCompositionEventArgs e) {
    //  if (e.Text.Length!=1) {
    //    System.Diagnostics.Debugger.Break();
    //  }
    //  var c = e.Text[0];
    //  e.Handled = c<'0' || c>'9';
    //}


    private void findWindow_Loaded(object sender, RoutedEventArgs e) {
      TextTextbox.Focus();
    }


    private void textTextbox_TextChanged(object sender, TextChangedEventArgs e) {
      var isEnabbled = TextTextbox.Text.Length>0;
      NextButton.IsEnabled = isEnabbled;
      PreviousButton.IsEnabled = isEnabbled;
    }


    public void FindNext() {
      var stringComparison =
          IgnoreCaseCheckBox.IsChecked!.Value ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
      if (searchedTextBox is not null) {
        if (TextTextbox.Text.Length==0) return;

        var actualPosition = searchedTextBox.SelectionStart + 1;
        actualPosition = searchedTextBox.Text.IndexOf(TextTextbox.Text, actualPosition, stringComparison);
        if (actualPosition<0) {
          actualPosition = searchedTextBox.Text.IndexOf(TextTextbox.Text, stringComparison);
          if (actualPosition<0) {
            MessageBox.Show($"Could not find '{TextTextbox.Text}'.", "Not found");
            return;
          }
        }
        searchedTextBox.Focus();
        searchedTextBox.Select(actualPosition, TextTextbox.Text.Length);

      } else if (searchedTextViewer is not null) {
        searchedTextViewer.Search(TextTextbox.Text, isForward: true, IgnoreCaseCheckBox.IsChecked!.Value);

      } else if (searchedRichTextBox is not null){
        TextPointer startTextPointer;
        bool isSearchFromMiddle;
        if (searchedRichTextBox!.Selection.IsEmpty) {
          startTextPointer = searchedRichTextBox.Document.ContentStart;
          isSearchFromMiddle = false;
        } else {
          startTextPointer = searchedRichTextBox.Selection.End;
          isSearchFromMiddle = true;
        }
        TextRange searchRange = new TextRange(startTextPointer, searchedRichTextBox.Document.ContentEnd);
        int offset = searchRange.Text.IndexOf(TextTextbox.Text, stringComparison);
        if (offset>0) {
          var start = GetTextPositionAtOffset(searchRange.Start, offset);
          var end = GetTextPositionAtOffset(start!, TextTextbox.Text.Length);
          searchedRichTextBox.Focus();
          searchedRichTextBox.Selection.Select(start, end);
          //((FrameworkContentElement)end!.Parent).BringIntoView();
          return;

        } else if(isSearchFromMiddle) {
          searchRange = new TextRange(searchedRichTextBox.Document.ContentStart, searchedRichTextBox.Selection.End);
          offset = searchRange.Text.IndexOf(TextTextbox.Text, stringComparison);
          if (offset>0) {
            var start = GetTextPositionAtOffset(searchRange.Start, offset);
            searchedRichTextBox.Focus();
            searchedRichTextBox.Selection.Select(start, GetTextPositionAtOffset(start!, TextTextbox.Text.Length));
            return;
          }
        }
        MessageBox.Show($"Could not find '{TextTextbox.Text}'.", "Not found");
        searchedRichTextBox.Focus();
        return;
      }
    }


    TextPointer? GetTextPositionAtOffset(TextPointer position, int characterCount) {
      while (position != null) {
        if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text) {
          int count = position.GetTextRunLength(LogicalDirection.Forward);
          if (characterCount <= count) {
            return position.GetPositionAtOffset(characterCount);
          }

          characterCount -= count;
        }

        TextPointer nextContextPosition = position.GetNextContextPosition(LogicalDirection.Forward);
        if (nextContextPosition == null)
          return position;

        position = nextContextPosition;
      }
      return position;
    }


    private void nextButton_Click(object sender, RoutedEventArgs e) {
      FindNext();
    }


    private void previousButton_Click(object sender, RoutedEventArgs e) {
      var stringComparison =
        IgnoreCaseCheckBox.IsChecked!.Value ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
      if (searchedTextBox!=null) {
        if (TextTextbox.Text.Length==0) return;

        var actualPosition = searchedTextBox.SelectionStart -1;
        if (actualPosition>=0) {
          actualPosition = searchedTextBox.Text.LastIndexOf(TextTextbox.Text, actualPosition, stringComparison);
        }
        if (actualPosition<0) {
          actualPosition = searchedTextBox.Text.LastIndexOf(TextTextbox.Text, stringComparison);
          if (actualPosition<0) {
            MessageBox.Show($"Could not find '{TextTextbox.Text}'.", "Not found");
            return;
          }
        }
        searchedTextBox.Focus();
        searchedTextBox.Select(actualPosition, TextTextbox.Text.Length);

      } else if (searchedTextViewer is not null) {
        searchedTextViewer.Search(TextTextbox.Text, isForward: false, IgnoreCaseCheckBox.IsChecked!.Value);

      } else {
        TextPointer endTextPointer;
        bool isSearchFromMiddle;
        if (searchedRichTextBox!.Selection.IsEmpty) {
          endTextPointer = searchedRichTextBox.Document.ContentEnd;
          isSearchFromMiddle = false;
        } else {
          endTextPointer = searchedRichTextBox.Selection.Start;
          isSearchFromMiddle = true;
        }
        TextRange searchRange = new TextRange(searchedRichTextBox.Document.ContentStart, endTextPointer);
        int offset = searchRange.Text.LastIndexOf(TextTextbox.Text, stringComparison);
        if (offset>0) {
          var start = GetTextPositionAtOffset(searchRange.Start, offset);
          searchedRichTextBox.Focus();
          searchedRichTextBox.Selection.Select(start, GetTextPositionAtOffset(start!, TextTextbox.Text.Length));
          //searchedRichTextBox.BringIntoView();
          //searchedRichTextBox.Selection.Start.Paragraph.BringIntoView();
          return;

        } else if (isSearchFromMiddle) {
          searchRange = new TextRange(searchedRichTextBox.Selection.Start, searchedRichTextBox.Document.ContentEnd);
          offset = searchRange.Text.LastIndexOf(TextTextbox.Text, stringComparison);
          if (offset>0) {
            var start = GetTextPositionAtOffset(searchRange.Start, offset);
            searchedRichTextBox.Focus();
            searchedRichTextBox.Selection.Select(start, GetTextPositionAtOffset(start!, TextTextbox.Text.Length));
            //searchedRichTextBox.Selection.Start.Paragraph.BringIntoView();
            return;
          }
        }
        MessageBox.Show($"Could not find '{TextTextbox.Text}'.", "Not found");
        searchedRichTextBox.Focus();
        return;

      }
    }


    private void findWindow_Closed(object? sender, EventArgs e) {
      callOwnerWhenClosing();
      Owner.Activate();
    }
  }
}
