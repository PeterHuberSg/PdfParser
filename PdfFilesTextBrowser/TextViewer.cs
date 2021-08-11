﻿using CustomControlBaseLib;
using PdfParserLib;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;


namespace PdfFilesTextBrowser {


  public class TextViewer: CustomControlBase {

    #region Properties
    //      ----------

    public readonly TextStore TextStore = new();


    public int LinesPerPage { get; private set; }
    
    
    public IReadOnlyList<double> DisplayedGlyphWidths => textViewerGlyph.DisplayedGlyphWidths;


    public IReadOnlyDictionary<string, TextViewerAnchor> Anchors { get { return anchors; } }


    readonly Dictionary<string, TextViewerAnchor> anchors = new();


    public readonly TextViewerObjects TextViewerObjects = new();


    public readonly TextViewerSelection TextViewerSelection; //highlights the characters presently selected


    public readonly Window OwnerWindow;
    #endregion


    #region Constructor
    //      -----------

    readonly ScrollBar verticalScrollBar;
    readonly ScrollBar horizontalScrollBar;
    readonly ZoomButton zoomButton;
    readonly TextViewerGlyph textViewerGlyph; //draws the characters
    readonly MenuItem zoomInMenuItem;
    readonly MenuItem zoomResetMenuItem;
    readonly MenuItem zoomOutMenuItem;


    /// <summary>
    /// Default constructor
    /// </summary>
    public TextViewer(Window ownerWindow) {
      OwnerWindow = ownerWindow;

      verticalScrollBar = new ScrollBar {
        Orientation= Orientation.Vertical,
        VerticalAlignment = VerticalAlignment.Stretch,
      };


      AddChild(verticalScrollBar);

      horizontalScrollBar = new ScrollBar {
        Orientation= Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Stretch,
      };
      AddChild(horizontalScrollBar);

      zoomButton = new ZoomButton(zoomIn, zoomOut);
      AddChild(zoomButton);

      //create textViewerGlyph first, because textViewerGlyph.DisplayedGlyphWidths is used in the textViewerSelection constructor
      textViewerGlyph = new(this, textViewerGlyphMaxLineWidthChanged);
      TextViewerSelection = new(this);
      //add textViewerSelection first as child, because it has to be under textViewerGlyph.
      AddChild(TextViewerSelection);
      AddChild(textViewerGlyph);

      Loaded += TextViewer_Loaded;
      //SizeChanged += TextViewer_SizeChanged;
      MouseWheel += TextViewer_MouseWheel;
      MouseEnter += TextViewer_MouseEnter;
      MouseMove += TextViewer_MouseMove;
      MouseDown += TextViewer_MouseDown;
      MouseUp += TextViewer_MouseUp;
      KeyDown += TextViewer_KeyDown;
      
      verticalScrollBar.ValueChanged += VerticalScrollBar_ValueChanged;
      horizontalScrollBar.ValueChanged += HorizontalScrollBar_ValueChanged;

      ContextMenu = new();
      var findMenuItem = new MenuItem { Header = "Find", InputGestureText = "Ctrl+'F'" };
      findMenuItem.Click += FindMenuItem_Click;
      ContextMenu.Items.Add(findMenuItem);
      var selectAllMenuItem = new MenuItem { Header = "Select all", InputGestureText = "Ctrl+'A'" };
      selectAllMenuItem.Click += SelectAllMenuItem_Click;
      ContextMenu.Items.Add(selectAllMenuItem);
      var unselectMenuItem = new MenuItem { Header = "Unselect", InputGestureText = "Ctrl+'U'" };
      unselectMenuItem.Click += UnselectMenuItem_Click;
      ContextMenu.Items.Add(unselectMenuItem);
      var copyMenuItem = new MenuItem { Header = "Copy", InputGestureText = "Ctrl+'C'" };
      copyMenuItem.Click += CopyMenuItem_Click;
      ContextMenu.Items.Add(copyMenuItem);

      ContextMenu.Items.Add(new Separator());
      
      zoomInMenuItem = new MenuItem { Header = "Zoom In", InputGestureText = "Ctrl+'+'" };
      zoomInMenuItem.Click += ZoomInMenuItem_Click;
      ContextMenu.Items.Add(zoomInMenuItem);
      zoomResetMenuItem = new MenuItem { Header = "Zoom Reset", InputGestureText = "Ctrl+'1'" };
      zoomResetMenuItem.Click += ZoomResetMenuItem_Click;
      ContextMenu.Items.Add(zoomResetMenuItem);
      zoomOutMenuItem = new MenuItem { Header = "Zoom Out", InputGestureText = "Ctrl+'-'" };
      zoomOutMenuItem.Click += zoomOutMenuItem_Click;
      ContextMenu.Items.Add(zoomOutMenuItem);
    }
    #endregion


    #region Eventhandlers
    //      -------------

    private void TextViewer_Loaded(object sender, RoutedEventArgs e) {
      Focus();//required for TextViewer_KeyDown to work
    }


    //bool isFirstTime = true;


    //private void TextViewer_SizeChanged(object sender, SizeChangedEventArgs e) {
    //  System.Diagnostics.Debug.WriteLine(Environment.NewLine);
    //  if (isFirstTime) {
    //    LogLine($"First time TextViewer_SizeChanged with forced InvalidateVisual()");
    //  }
    //  LogLine($"TextViewer_SizeChanged ActualWidth: {ActualWidth:F0}; ActualHeight: {ActualHeight:F0};");
    //  resize();
    //  if (isFirstTime) {
    //    isFirstTime = false;
    //    InvalidateVisual();
    //  }
    //}


    //private void resize() {
    //  var availableHeight = ActualHeight-ScrollbarThickness;
    //  if (availableHeight<FontSize) return; //not enough space to display any text

    //  LinesPerPage = (int)Math.Floor((availableHeight) / FontSize);
    //  LogLine(
    //    $"resize ActualHeight: {ActualHeight:F0}, FontSize: {FontSize}, TextStore.LinesCount: {TextStore.LinesCount}, LinesPerPage: {LinesPerPage}");
    //  verticalScrollBar.LargeChange = verticalScrollBar.ViewportSize = LinesPerPage;
    //  verticalScrollBar.Maximum = TextStore.LinesCount-LinesPerPage + 1;
    //  LogLine(
    //    $"verticalScrollBar Maximum: {verticalScrollBar.Maximum:F0}, LargeChange: {verticalScrollBar.LargeChange:F0}; Value: {verticalScrollBar.Value:F0}");
    //  textViewerGlyph.SetDisplayLines(new DisplayLines((int)verticalScrollBar.Value, LinesPerPage));
    //  //textViewerGlyph.ResetMaxLineWidth();
    //  zoomButton.IsZoomInEnabled = zoomInMenuItem.IsEnabled = zoomStep*FontSize<=ActualHeight/2;
    //  zoomButton.IsZoomOutEnabled = zoomOutMenuItem.IsEnabled = FontSize>=zoomStep*zoomMinFontSize;
    //}


    private void TextViewer_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) {
      if (e.Delta<0) {
        verticalScrollBar.Value = Math.Min(verticalScrollBar.Maximum, verticalScrollBar.Value+5);
      } else {
        verticalScrollBar.Value = Math.Max(0, verticalScrollBar.Value-5);
      }
      //LogLine2($"TextViewer_MouseWheel scrollBar.Value: {verticalScrollBar.Value:F0}");
    }


    private void TextViewer_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) {
      Focus();//needed to receive keyboard events
    }


    private void TextViewer_MouseMove(object sender, MouseEventArgs e) {
      UpdateMouseCursorAndSelection();
    }


    public void UpdateMouseCursorAndSelection() {
      if (textViewerGlyph.DisplayLines is null) return;

      var position = Mouse.GetPosition(this);
      if (position.X<0 || position.X>ActualWidth || position.Y<0 || position.Y>ActualHeight) return;//Mouse not in TextViewer

      //update how mouse cursor looks
      var mouseDisplayLine = Math.Min((int)(position.Y / FontSize), LinesPerPage-1);
      var textViewerObject = TextViewerObjects.GetObjectForDisplayLine(mouseDisplayLine, position.X);
      if (textViewerObject is null) {
        Cursor = Cursors.Arrow;
      } else {
        Cursor = Cursors.Hand;
      }

      //update selected characters if mouse is down
      if (Mouse.LeftButton!=MouseButtonState.Pressed) {
        //mouse might have been released outside TextViewer
        isMouseDown = false;
      } else if (isMouseDown) {
        var currentMouseAbsolutLine = Math.Min(mouseDisplayLine + textViewerGlyph.DisplayLines.StartAbsoluteLine, TextStore.LinesCount -1);
        var currentCharPosLeft = TextViewerSelection.GetCharPosLeft(ref currentMouseAbsolutLine, position.X + textViewerGlyph.XOffset - TextViewerGlyph.BorderX);

        if (mouseDownStartAbsoluteLine<currentMouseAbsolutLine) {
          TextViewerSelection.SetSelection(new TextStoreSelection(TextStore, mouseDownStartAbsoluteLine, 
            mouseDownStartCharPosLeft, currentMouseAbsolutLine, currentCharPosLeft), isImmediateRenderingNeeded: true);
        } else if (mouseDownStartAbsoluteLine>currentMouseAbsolutLine) {
          TextViewerSelection.SetSelection(new TextStoreSelection(TextStore, currentMouseAbsolutLine, currentCharPosLeft, 
            mouseDownStartAbsoluteLine, mouseDownStartCharPosLeft), isImmediateRenderingNeeded: true);
        } else {
          //mouseDownStartAbsoluteLine==currentAbsolutLine
          if (mouseDownStartCharPosLeft<currentCharPosLeft) {
            TextViewerSelection.SetSelection(new TextStoreSelection(TextStore, mouseDownStartAbsoluteLine, 
              mouseDownStartCharPosLeft, currentMouseAbsolutLine, currentCharPosLeft), isImmediateRenderingNeeded: true);
          } else {
            TextViewerSelection.SetSelection(new TextStoreSelection(TextStore, currentMouseAbsolutLine, currentCharPosLeft, 
              mouseDownStartAbsoluteLine, mouseDownStartCharPosLeft), isImmediateRenderingNeeded: true);
          }
        }
      }
    }


    PdfStreamWindow? streamWindow;
    StringBuilder streamWindowStringBuilder = new();


    public void ResetStreamWindow() { 
      streamWindow = null; 
    }


    bool isMouseDown;
    int mouseDownStartAbsoluteLine;
    int mouseDownStartCharPosLeft;


    private void TextViewer_MouseDown(object sender, MouseButtonEventArgs e) {
      if (textViewerGlyph.DisplayLines is null) return;
      /////////////////////
      //var sb = new StringBuilder();
      ////for (int charIndex = 0; charIndex<TextStore.CharsCount; charIndex++) {
      //for (int charIndex = 0; charIndex<textViewerGlyph.DisplayedGlyphWidths.Count; charIndex++) {
      //  sb.AppendLine($"{TextStore.Chars[charIndex]} {textViewerGlyph.DisplayedGlyphWidths[charIndex]}");
      //}
      //var s = sb.ToString();
      /////////////////////

      var mouseDownStartPosition = Mouse.GetPosition(this);
      var mouseDownStartLine = Math.Min((int)(mouseDownStartPosition.Y / FontSize), LinesPerPage-1);
      mouseDownStartAbsoluteLine = Math.Min(mouseDownStartLine + textViewerGlyph.DisplayLines.StartAbsoluteLine, TextStore.LinesCount -1);
      var absoluteX = mouseDownStartPosition.X + textViewerGlyph.XOffset;

      var textViewerObject = TextViewerObjects.GetObjectForDisplayLine(mouseDownStartLine, absoluteX);
      if (textViewerObject is not null) {
        if (textViewerObject.IsLink) {
          textViewerGlyph.SetMarker(textViewerObject.Anchor);
          verticalScrollBar.Value = Math.Min(Math.Max(textViewerObject.Anchor!.Line-3, 0), verticalScrollBar.Maximum);
          //LogLine2($"TextViewer_MouseDown scrollBar.Value: {verticalScrollBar.Value:F0}");
          InvalidateVisual(); //needs to force a redraw even if exactly the same page gets displayed, so that the anchor 
                              //marker gets drawn
        } else if (textViewerObject.IsStream) {
          if (streamWindow is null) {
            streamWindow = new PdfStreamWindow(tokeniser, streamWindowStringBuilder, textViewerObject.ObjectId, this);
          } else {
            streamWindow.Update(textViewerObject.ObjectId);
          }

        } else {
          System.Diagnostics.Debugger.Break();
          throw new NotSupportedException();
        }
      } else {
        isMouseDown = true;
        mouseDownStartCharPosLeft = TextViewerSelection.GetCharPosLeft(ref mouseDownStartAbsoluteLine, absoluteX);
      }
    }


    private void TextViewer_MouseUp(object sender, MouseButtonEventArgs e) {
      isMouseDown = false;
    }


    private void TextViewer_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
      if (e.Key!=Key.LeftCtrl && e.Key!=Key.RightCtrl) {
        var keyString = e.SystemKey==Key.None ? e.Key.ToString() : e.SystemKey.ToString();
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) {
          keyString = "Ctrl + " + keyString;
        }
        //LogLine2Skip2Line($"KeyDown {keyString}; KeyStates: {e.KeyStates}");
      }
      if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) {
        //control key pressed
        switch (e.Key) {
        //Ctrl f is handled in MainWindow
        case Key.A: SetSelection(TextStore.SelectAll()); break;
        case Key.U: SetSelection(null); break;
        case Key.C: copyToClipboard(); break;
        case Key.Add:
        case Key.OemPlus: zoomIn(); break;
        case Key.NumPad1:
        case Key.D1: zoomReset(); break;
        case Key.Subtract:
        case Key.OemMinus: zoomOut(); break;
        case Key.NumPad9:
        case Key.PageUp: horizontalScrollBar.Value += horizontalScrollBar.LargeChange; break;
        case Key.NumPad3:
        case Key.PageDown: horizontalScrollBar.Value -= horizontalScrollBar.LargeChange; break;

        default: break;
        }
      } else {
        //control key not pressed
        switch (e.Key) {
        case Key.NumPad8:
        case Key.Down: verticalScrollBar.Value = Math.Max(verticalScrollBar.Minimum, verticalScrollBar.Value - 1); break;
        case Key.NumPad2:
        case Key.Up: verticalScrollBar.Value = Math.Min(verticalScrollBar.Maximum, verticalScrollBar.Value + 1); break;
        case Key.NumPad7:
        case Key.Home: verticalScrollBar.Value = verticalScrollBar.Minimum; break;
        case Key.NumPad1:
        case Key.End: verticalScrollBar.Value = verticalScrollBar.Maximum; break;
        case Key.NumPad9:
        case Key.PageUp: verticalScrollBar.Value = Math.Max(verticalScrollBar.Minimum, verticalScrollBar.Value - LinesPerPage); break;
        case Key.NumPad3:
        case Key.PageDown: verticalScrollBar.Value = Math.Min(verticalScrollBar.Maximum, verticalScrollBar.Value + LinesPerPage); break;
        default:
          break;
        }
      }
    }


    private void VerticalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
      if (textViewerGlyph.DisplayLines is null) return;

      if (textViewerGlyph.DisplayLines.StartAbsoluteLine!=verticalScrollBar.Value) {
        //LogLine($"VerticalScrollBar_ValueChanged: {verticalScrollBar.Value}");
      }
      textViewerGlyph.SetDisplayLines(new DisplayLines((int)verticalScrollBar.Value, LinesPerPage));
    }


    double maxLineWidthZoomFactor1;//number of pixels needed to display the widest TextStore line displayed so far at zoomFactor 1


    private void textViewerGlyphMaxLineWidthChanged() {
      var newMaxLineWidth = Math.Max(maxLineWidthZoomFactor1, textViewerGlyph.MaxLineWidth/zoomFactor);
      if (maxLineWidthZoomFactor1!=newMaxLineWidth) {
        maxLineWidthZoomFactor1 = newMaxLineWidth;
        //LogLine($"TextViewer_MaxLineWidthChanged MaxLineWidth: {textViewerGlyph.MaxLineWidth:F0}; " +
        //  $"zoomFactor {zoomFactor}; maxLineWidthZoomFactor1: {maxLineWidthZoomFactor1:F0}");
        InvalidateVisual();
      }
    }


    //private void setHorizontalScrollBar(double value) {
    //  //horizontalScrollBar.Value is the pixel offset from left border
    //  //horizontalScrollBar.Maximum + horizontalScrollBar.LargeChange is length of longest line displayed so far
    //  var availableDisplayWidth = ActualWidth-ScrollbarThickness;
    //  if (availableDisplayWidth<0) return; //not enough width to display horizontal scrollbar

    //  horizontalScrollBar.ViewportSize = horizontalScrollBar.LargeChange = availableDisplayWidth;
    //  horizontalScrollBar.SmallChange = horizontalScrollBar.LargeChange / 10;
    //  horizontalScrollBar.Maximum = Math.Max(0, maxLineWidthZoomFactor1*zoomFactor - availableDisplayWidth);
    //  LogLineSkip2Line(
    //  $"resizeHorizontalScrollBar ActualWidth: {ActualWidth:F0}; LargeChange: {horizontalScrollBar.LargeChange:F0}; " + 
    //  $"maxLineWidthZoomFactor1: {maxLineWidthZoomFactor1:F0}; " +
    //  $"zoomFactor: {zoomFactor}; maxLineWidthZoomFactor1*zoomFactor: {maxLineWidthZoomFactor1*zoomFactor:F0}; " +
    //  $"Value: {horizontalScrollBar.Value:F0}");
    //}


    private void HorizontalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
      //LogLine2($"HorizontalScrollBar_ValueChanged: {horizontalScrollBar.Value:F0}; ActualWidth: {ActualWidth:F0}; " +
      //  $"MaxLineWidth: {textViewerGlyph.MaxLineWidth:F0};");
      textViewerGlyph.SetXOffset(horizontalScrollBar.Value*zoomFactor);
    }


    private void FindMenuItem_Click(object sender, RoutedEventArgs e) {
      if (OwnerWindow is MainWindow mainWindow) {//should always be true
        mainWindow.OpenFindWindow();
      }
    }


    private void SelectAllMenuItem_Click(object sender, RoutedEventArgs e) {
      SetSelection(TextStore.SelectAll());
    }


    private void UnselectMenuItem_Click(object sender, RoutedEventArgs e) {
      SetSelection(null);
    }


    private void CopyMenuItem_Click(object sender, RoutedEventArgs e) {
      copyToClipboard();
    }


    private void copyToClipboard() {
      if (TextViewerSelection.Selection is not null) {
        Clipboard.SetText(TextViewerSelection.Selection.GetContent());
      }
    }


    const double zoomStep = 2.0;
    const double zoomMinFontSize = 4.0;
    double zoomFactor;


    private void ZoomInMenuItem_Click(object sender, RoutedEventArgs e) {
      zoomIn();
    }


    private void zoomIn() {
      var newFontSize = FontSize * zoomStep;
      if (newFontSize<=ActualHeight/2) {
        zoomFactor *= zoomStep;
        FontSize = newFontSize;
        //LogLine2($"zoomIn zoomFactor: {zoomFactor}; Font: {FontSize};");
        //setHorizontalScrollBar();
        //horizontalScrollBar.Value = Math.Min(horizontalScrollBar.Maximum, horizontalScrollBar.Value * zoomStep);
        //LogLine($"zoomIn hScrollBar: {horizontalScrollBar.Value:F0};");
        //textViewerGlyph.SetXOffset(horizontalScrollBar.Value*zoomFactor);
        //resize();
        InvalidateVisual();
        //if (TextViewerSelection.Selection is not null) {
        //  var selection = TextViewerSelection.Selection;
        //  SetSelection(null);
        //  SetSelection(selection);
        //}
        updateZoomButtonEnabled();
      }
    }


    private void updateZoomButtonEnabled() {
      zoomButton.IsZoomInEnabled = zoomInMenuItem.IsEnabled = zoomStep*FontSize<=ActualHeight/2;
      zoomButton.IsZoomOutEnabled = zoomOutMenuItem.IsEnabled = FontSize>=zoomStep*zoomMinFontSize;
    }


    private void ZoomResetMenuItem_Click(object sender, RoutedEventArgs e) {
      zoomReset();
    }


    private void zoomReset() {
      //calculate new horizontalScrollBar.Value based on old zoomFactor
      //it might not be possible to set the new horizontalScrollBar.Value already here, because when zooming in Maximum
      //might be too small to allow it. Maximum gets adjusted in setHorizontalScrollBar()
      //var newHorizontalScrollBarValue = horizontalScrollBar.Value / zoomFactor;
      FontSize = ((Control)Parent).FontSize;
      zoomFactor = 1;
      //LogLine2($"zoomReset zoomFactor: {zoomFactor}; Font: {FontSize};");
      //textViewerGlyph.SetXOffset(horizontalScrollBar.Value*zoomFactor);
      //setHorizontalScrollBar();
      //horizontalScrollBar.Value = newHorizontalScrollBarValue;
      //LogLine($"zoomReset hScrollBar: {horizontalScrollBar.Value:F0};");
      //resize();
      InvalidateVisual();
      updateZoomButtonEnabled();
    }


    private void zoomOutMenuItem_Click(object sender, RoutedEventArgs e) {
      zoomOut();
    }


    private void zoomOut() {
      var newFontSize = FontSize / zoomStep;
      if (newFontSize>=zoomMinFontSize) {
        zoomFactor /= zoomStep;
        FontSize = newFontSize;
        //LogLine2($"zoomOut zoomFactor: {zoomFactor}; Font: {FontSize};");
        //textViewerGlyph.SetXOffset(horizontalScrollBar.Value*zoomFactor);
        //var horizontalScrollBarValue = horizontalScrollBar.Value;//need to store this value temporarioly because setHorizontalScrollBar
        ////changes Maximum, which might change Value
        //setHorizontalScrollBar();
        //horizontalScrollBar.Value = horizontalScrollBarValue/zoomStep;
        //LogLine($"zoomOut hScrollBar: {horizontalScrollBar.Value:F0};");
        //resize();
        InvalidateVisual();
        updateZoomButtonEnabled();
      }
    }
    #endregion


    #region TextStore
    //      ---------

    Tokeniser tokeniser;

    public void Load(Tokeniser tokeniser) {
      this.tokeniser = tokeniser;
      zoomFactor = 1;
      TextStore.Reset();
      textViewerGlyph.Reset();
      TextViewerSelection.Reset();
      anchors.Clear();
      PdfToTextStore.Convert(tokeniser, TextStore, anchors);

      ///////////////////
      //TextStore.Append(new byte[] { (byte)'W', (byte)'n', (byte)'m', (byte)'d', (byte)'e', (byte)'f', (byte)'g', (byte)'h', (byte)'i', (byte)'j', (byte)'k', (byte)'l', (byte)'m', (byte)'n', (byte)'o', (byte)'\r' });
      //TextStore.Append(new byte[] { (byte)'i', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', (byte)'G', (byte)'H', (byte)'I', (byte)'J', (byte)'K', (byte)'L', (byte)'M', (byte)'N', (byte)'O', (byte)'\r' });
      //TextStore.Append(new byte[] { (byte)'i', (byte)'i', (byte)'1', (byte)'2', (byte)'3', (byte)'{', (byte)'b', (byte)'4', (byte)'5', (byte)'6', (byte)'}', (byte)'7', (byte)'8', (byte)'9', (byte)'\r' });
      //TextStore.Append(new byte[] { (byte)'\r' });
      //TextStore.Append(new byte[] { (byte)'i', (byte)'i', (byte)'i', (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e', (byte)'f', (byte)'g', (byte)'h', (byte)'i', (byte)'j', (byte)'k', (byte)'l', (byte)'m', (byte)'n', (byte)'o', (byte)'\r' });
      //TextStore.Append(new byte[] { (byte)'{', (byte)'u', (byte)'i', (byte)'}', (byte)'i', (byte)'{', (byte)'u', (byte)'i', (byte)'i', (byte)'}', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', (byte)'{', (byte)'u', (byte)'G', (byte)'H', (byte)'I', (byte)'}', (byte)'J', (byte)'K', (byte)'L', (byte)'M', (byte)'N', (byte)'O', (byte)'\r' });
      //TextStore.Append(new byte[] { (byte)'1', (byte)'\r', (byte)'2', (byte)'\r', (byte)'3', (byte)'\r', (byte)'4', (byte)'\r', (byte)'5', (byte)'\r', (byte)'6', (byte)'\r', (byte)'7', (byte)'\r', (byte)'8', (byte)'\r', (byte)'9', (byte)'\r', (byte)'0', (byte)'\r' });
      //TextStore.Append(new byte[] { (byte)'i', (byte)'i', (byte)'i', (byte)'i', (byte)'i', (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e', (byte)'f', (byte)'g', (byte)'h', (byte)'i', (byte)'j', (byte)'k', (byte)'l', (byte)'m', (byte)'n', (byte)'o', (byte)'\r' });
      //TextStore.Append(new byte[] { (byte)'i', (byte)'i', (byte)'i', (byte)'i', (byte)'i', (byte)'i', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', (byte)'G', (byte)'H', (byte)'I', (byte)'J', (byte)'K', (byte)'L', (byte)'M', (byte)'N', (byte)'O', (byte)'\r' });
      //TextStore.Append(new byte[] { (byte)'1', (byte)'\r', (byte)'2', (byte)'\r', (byte)'3', (byte)'\r', (byte)'4', (byte)'\r', (byte)'5', (byte)'\r', (byte)'6', (byte)'\r', (byte)'7', (byte)'\r', (byte)'8', (byte)'\r', (byte)'9', (byte)'\r', (byte)'0', (byte)'\r' });
      //TextStore.Append(new byte[] { (byte)'i', (byte)'i', (byte)'1', (byte)'2', (byte)'3', (byte)'{', (byte)'b', (byte)'4', (byte)'5', (byte)'6', (byte)'}', (byte)'7', (byte)'8', (byte)'9', (byte)'\r' });
      ///////////////////
      //verticalScrollBar.Maximum = TextStore.LinesCount-LinesPerPage + 1;
      //verticalScrollBar.Value = 0;
      InvalidateVisual();
      //Log($"Load() scrollBar.Value: {scrollBar.Value}");
    }


    private bool getNext(out byte nextByte, ReadOnlySpan<byte> text, ref int charIndex) {
      charIndex++;
      if (charIndex>=text.Length) {
        nextByte = 0;
        return false;
      }

      nextByte = text[charIndex];
      return true;
    }
    #endregion


    #region Selection
    //      ---------


    public void SetSelection(TextStoreSelection? selection) {
      if (textViewerGlyph.DisplayLines is null) return;

      if (selection is null) {
        TextViewerSelection.SetSelection(null, isImmediateRenderingNeeded: true);
        return;
      }

      var isNoScrolling = true;
      if (selection.StartLine<textViewerGlyph.DisplayLines.StartAbsoluteLine ||
        selection.StartLine>textViewerGlyph.DisplayLines.EndAbsoluteLine) 
        {
        //selection does not start within displayed lines.
        verticalScrollBar.Value = Math.Min(Math.Max(selection.StartLine-3, 0), verticalScrollBar.Maximum);
        isNoScrolling = false;
      }
      var startX = textViewerGlyph.GetStartX(selection);
      if (startX<textViewerGlyph.XOffset || startX>textViewerGlyph.XOffset+ActualWidth) {
        //selection starts to the left or the right of the displayed lines.
        startX = Math.Min(Math.Max(startX-2*FontSize, 0), horizontalScrollBar.Maximum);

        //horizontalScrollBar.Value = Math.Min(startX, horizontalScrollBar.Maximum);
        InvalidateVisual();
        isNoScrolling = false;
      }
      TextViewerSelection.SetSelection(selection, isImmediateRenderingNeeded: isNoScrolling);

    }


    internal void Search(string searchString, bool isForward, bool isIgnoreCase) {
      var selection = TextStore.FindString(TextViewerSelection.Selection, searchString, isForward, isIgnoreCase);
      if (selection is null) return;

      SetSelection(selection);
    }


    //public void LogLine1(string text) {
    //  System.Diagnostics.Debug.WriteLine("");
    //  LogLine(text);
    //}


    //public void LogLine2(string text) {
    //  //if (isSkip2Lines) {
    //  //  isSkip2Lines = false;
    //  //} else {
    //  //  System.Diagnostics.Debug.WriteLine(Environment.NewLine);
    //  //}
    //  LogLine(text);
    //}


    //bool isSkip2Lines;


    //public void LogLine2Skip2Line(string text) {
    //  //System.Diagnostics.Debug.WriteLine(Environment.NewLine);
    //  LogLine(text);
    //  //isSkip2Lines = true;
    //}


    //public void LogLineSkip2Line(string text) {
    //  LogLine(text);
    //  //isSkip2Lines = true;
    //}


    //DateTime lastNow;


    //public void LogLine(string text) {
    //  var now = DateTime.Now;
    //  if (now-lastNow>TimeSpan.FromMilliseconds(150)) {
    //    System.Diagnostics.Debug.WriteLine(Environment.NewLine);
    //  }
    //  lastNow = now;
    //  System.Diagnostics.Debug.WriteLine($"{now:mm.ss.fff}  {text}");
    //  isSkip2Lines = false;
    //}
    #endregion


    #region Measure, Arrange and Render
    //      ---------------------------

    protected override Size MeasureContentOverride(Size constraint) {
      verticalScrollBar.Measure(constraint);
      horizontalScrollBar.Measure(constraint);
      zoomButton.Measure(new Size(verticalScrollBar.DesiredSize.Width, horizontalScrollBar.DesiredSize.Height));
      var remainingWidth = constraint.Width - verticalScrollBar.DesiredSize.Width;
      var remainingHeight = constraint.Height - horizontalScrollBar.DesiredSize.Height;
      if (remainingWidth>0 && remainingHeight>0) {
        TextViewerSelection.Measure(new Size(remainingWidth, remainingHeight));
        textViewerGlyph.Measure(new Size(remainingWidth, remainingHeight));
      }
      return constraint;
    }


    protected override Size ArrangeContentOverride(Rect arrangeRect) {
      var verticalScrollBarX = arrangeRect.Width - verticalScrollBar.DesiredSize.Width;
      var horizontalScrollBarY = arrangeRect.Height - horizontalScrollBar.DesiredSize.Height;
      if (horizontalScrollBarY>0) {
        verticalScrollBar.ArrangeBorderPadding(arrangeRect, verticalScrollBarX, 0,
          verticalScrollBar.DesiredSize.Width, horizontalScrollBarY);
        setupVerticalScrollBar(horizontalScrollBarY);
      }
      if (verticalScrollBarX>0) {
        horizontalScrollBar.ArrangeBorderPadding(arrangeRect, 0, horizontalScrollBarY,
          verticalScrollBarX, horizontalScrollBar.DesiredSize.Height);

        setupHorizontalScrollBar(verticalScrollBarX);
      }
      zoomButton.ArrangeBorderPadding(arrangeRect, verticalScrollBarX, horizontalScrollBarY,
        verticalScrollBar.DesiredSize.Width, horizontalScrollBar.DesiredSize.Height);
      if (horizontalScrollBarY>0 && verticalScrollBarX>0) {
        TextViewerSelection.ArrangeBorderPadding(arrangeRect, 0, 0, verticalScrollBarX,
          horizontalScrollBarY);
        textViewerGlyph.ArrangeBorderPadding(arrangeRect, 0, 0, verticalScrollBarX, horizontalScrollBarY);
      }
      return arrangeRect.Size;
    }


    private void setupVerticalScrollBar(double pixelHeight) {
      if (pixelHeight<FontSize) return; //not enough space to display any text

      LinesPerPage = (int)Math.Floor(pixelHeight / FontSize);
      verticalScrollBar.LargeChange = verticalScrollBar.ViewportSize = LinesPerPage;
      verticalScrollBar.SmallChange = Math.Max(1, verticalScrollBar.LargeChange/10);
      verticalScrollBar.Maximum = TextStore.LinesCount-LinesPerPage + 1;
      //LogLine(
      //  $"ArrangeContentOverride VerticalScrollBar pixelHeight: {pixelHeight:F0}, FontSize: {FontSize}, LinesPerPage: {LinesPerPage}, " + 
      //  $"LargeChange: {verticalScrollBar.LargeChange}; TextStore.LinesCount: {TextStore.LinesCount}, " +
      //  $"verticalScrollBar Maximum: {verticalScrollBar.Maximum:F0}, Value: {verticalScrollBar.Value:F0}");
      textViewerGlyph.SetDisplayLines(new DisplayLines((int)verticalScrollBar.Value, LinesPerPage));
    }


    private void setupHorizontalScrollBar(double pixelWidth) {
      //horizontalScrollBar.Value is pixel offset from left border ZoomFactor=1
      horizontalScrollBar.ViewportSize = horizontalScrollBar.LargeChange = pixelWidth / zoomFactor;
      horizontalScrollBar.SmallChange = horizontalScrollBar.LargeChange / 10;
      horizontalScrollBar.Maximum = Math.Max(0, maxLineWidthZoomFactor1 - horizontalScrollBar.ViewportSize);
      //LogLineSkip2Line(
      //$"ArrangeContentOverride HorizontalScrollBar pixelWidth: {pixelWidth:F0}; zoomFactor: {zoomFactor}; " +
      //$"LargeChange: {horizontalScrollBar.LargeChange:F0}; " +
      //$"maxLineWidthZoomFactor1: {maxLineWidthZoomFactor1:F0}; Maximum: {horizontalScrollBar.Maximum:F0}; " +
      //$"Value: {horizontalScrollBar.Value:F0}");
      textViewerGlyph.SetXOffset(horizontalScrollBar.Value*zoomFactor);
    }


    protected override void OnRenderContent(DrawingContext drawingContext, Size renderContentSize) {
      //LogLine("TextViewer.OnRenderContent");
    }
    #endregion
  }
}
