using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PdfFilesTextBrowser {


  public class ZoomButton: Button {


    public bool IsZoomInEnabled {
      get {
        return isZoomInEnabled;
      }
      set {
        if (isZoomInEnabled!=value) {
          isZoomInEnabled = value;
          if (zoomButtonGraph is not null) {
            zoomButtonGraph.IsZoomInEnabled = value;
          }
        }
      }
    }
    bool isZoomInEnabled;


    public bool IsZoomOutEnabled {
      get {
        return isZoomOutEnabled;
      }
      set {
        if (isZoomOutEnabled!=value) {
          isZoomOutEnabled = value;
          if (zoomButtonGraph is not null) {
            zoomButtonGraph.IsZoomOutEnabled = value;
          }
        }
      }
    }
    bool isZoomOutEnabled;


    readonly Action zoomIn, zoomOut;


    public ZoomButton(Action zoomIn, Action zoomOut) {
      this.zoomIn = zoomIn;
      this.zoomOut = zoomOut;
      Loaded += ZoomButton_Loaded;
      Click += ZoomButton_Click;
    }


    ZoomButtonGraph? zoomButtonGraph;


    private void ZoomButton_Loaded(object sender, System.Windows.RoutedEventArgs e) {
      HorizontalAlignment = HorizontalAlignment.Stretch;
      VerticalAlignment = VerticalAlignment.Stretch;
      Height = 20;
      Width = 20;
      FontSize = 12;
      zoomButtonGraph = new ZoomButtonGraph(isZoomInEnabled, isZoomOutEnabled);
      Content = zoomButtonGraph;
    }



    private void ZoomButton_Click(object sender, RoutedEventArgs e) {
      const int borderThickness = 2 * 2;
      var position = Mouse.GetPosition(this);
      if (position.X/(ActualWidth-borderThickness) + position.Y/(ActualHeight-borderThickness)<1) {
        zoomIn();
      } else {
        zoomOut();
      }
    }
  }


  class ZoomButtonGraph: FrameworkElement {
    public bool IsZoomInEnabled {
      get {
        return isZoomInEnabled;
      }
      set {
        if (isZoomInEnabled!=value) {
          isZoomInEnabled = value;
          InvalidateVisual();
        }
      }
    }
    bool isZoomInEnabled;


    public bool IsZoomOutEnabled {
      get {
        return isZoomOutEnabled;
      }
      set {
        if (isZoomOutEnabled!=value) {
          isZoomOutEnabled = value;
          InvalidateVisual();
        }
      }
    }
    bool isZoomOutEnabled;


    public ZoomButtonGraph(bool isZoomInEnabled, bool isZoomOutEnabled) {
      this.isZoomInEnabled = isZoomInEnabled;
      this.isZoomOutEnabled = isZoomOutEnabled;
    }


    protected override Size MeasureOverride(Size availableSize) {
      return availableSize;
    }


    protected override Size ArrangeOverride(Size finalSize) {
      return finalSize;
    }



    static readonly Pen penEnabled = new Pen(Brushes.Black, 1);
    static readonly Pen penDisabled = new Pen(Brushes.Gray, 1);
    static readonly Pen penDiagonal = new Pen(Brushes.DarkSlateGray, 2);
    LinearGradientBrush leftTriangleBrush = new LinearGradientBrush(Color.FromArgb(0x80, 0xff, 0xff, 0xff), Color.FromArgb(0x80, 0x80, 0x80, 0x80), 45);
    LinearGradientBrush rightTriangleBrush = new LinearGradientBrush(Color.FromArgb(0x80, 0x40, 0x40, 0x40), Color.FromArgb(0x80, 0xff, 0xff, 0xff), 45);


    protected override void OnRender(DrawingContext drawingContext) {
      base.OnRender(drawingContext);
      var x = ActualWidth / 16;
      var y = ActualHeight /16;

      var triangleLeft = new PathGeometry();
      var leftFigure = new PathFigure { IsClosed = true };
      triangleLeft.Figures.Add(leftFigure);
      leftFigure.StartPoint = new Point(0, 0);
      leftFigure.Segments.Add(new LineSegment(new Point(0, ActualHeight), isStroked: true));
      leftFigure.Segments.Add(new LineSegment(new Point(ActualWidth, 0), isStroked: true));
      leftFigure.Segments.Add(new LineSegment(new Point(0, 0), isStroked: true));
      drawingContext.DrawGeometry(leftTriangleBrush, null, triangleLeft);

      var triangleRight = new PathGeometry();
      var rightFigure = new PathFigure { IsClosed = true };
      triangleRight.Figures.Add(rightFigure);
      rightFigure.StartPoint = new Point(ActualWidth, ActualHeight);
      rightFigure.Segments.Add(new LineSegment(new Point(0, ActualHeight), isStroked: true));
      rightFigure.Segments.Add(new LineSegment(new Point(ActualWidth, 0), isStroked: true));
      rightFigure.Segments.Add(new LineSegment(new Point(ActualWidth, ActualHeight), isStroked: true));
      drawingContext.DrawGeometry(rightTriangleBrush, null, triangleRight);

      drawingContext.DrawLine(penDiagonal, new Point(0, ActualHeight), new Point(ActualWidth, 0));
      if (isZoomInEnabled) {
        drawingContext.DrawLine(penEnabled, new Point(4*x, 1*y), new Point(4*x, 7*y));
        drawingContext.DrawLine(penEnabled, new Point(1*x, 4*y), new Point(7*x, 4*y));
      } else {
        drawingContext.DrawLine(penDisabled, new Point(4*x, 1*y), new Point(4*x, 7*y));
        drawingContext.DrawLine(penDisabled, new Point(1*x, 4*y), new Point(7*x, 4*y));
      }
      if (isZoomOutEnabled) {
        drawingContext.DrawLine(penEnabled, new Point(9*x, 12*y), new Point(15*x, 12*y));
      } else {
        drawingContext.DrawLine(penDisabled, new Point(9*x, 12*y), new Point(15*x, 12*y));
      }
    }
  }
}
