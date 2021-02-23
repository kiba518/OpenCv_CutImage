using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Rectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;

namespace WpfOpenCV
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑 cvextern
    /// </summary>
    public partial class MainWindow : Window
    { 
        public MainWindow()
        {
            InitializeComponent(); 
        }
        #region 矩形 
        private void btnRectangle_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog frm = new System.Windows.Forms.OpenFileDialog();
            frm.Filter = "(*.jpg,*.png,*.jpeg,*.bmp,*.gif)|*.jgp;*.png;*.jpeg;*.bmp;*.gif|All files(*.*)|*.*";
            if (frm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                CutRectangleImage(frm.FileName);
            } 
        }
        public void CutRectangleImage(string imagePath)
        {
            Image<Bgr, Byte> src = new Image<Bgr, byte>(imagePath);
            int scale = 1;
            if (src.Width > 500)
            {
                scale = 2;
            }
            if (src.Width > 1000)
            {
                scale = 10;
            }
            if (src.Width > 10000)
            {
                scale = 100;
            }
            var size = new Size(src.Width / scale, src.Height / scale);
            Image<Bgr, Byte> srcNewSize = new Image<Bgr, byte>(size);
            CvInvoke.Resize(src, srcNewSize, size);
            //将图像转换为灰度
            UMat grayImage = new UMat(); 
            CvInvoke.CvtColor(srcNewSize, grayImage, ColorConversion.Bgr2Gray);
            //使用高斯滤波去除噪声
            CvInvoke.GaussianBlur(grayImage, grayImage, new Size(3, 3), 3);
            UMat cannyEdges = new UMat();
            CvInvoke.Canny(grayImage, cannyEdges, 60, 180);//通过边缘化，然后取出轮廓
             
            #region 取三角形和矩形的顶点坐标
            List<Triangle2DF> triangleList = new List<Triangle2DF>();
            List<RotatedRect> boxList = new List<RotatedRect>(); //旋转的矩形框

            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
            {
                CvInvoke.FindContours(cannyEdges, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);
                int count = contours.Size;
                for (int i = 0; i < count; i++)
                {
                    using (VectorOfPoint contour = contours[i])
                    using (VectorOfPoint approxContour = new VectorOfPoint())
                    {
                        CvInvoke.ApproxPolyDP(contour, approxContour, CvInvoke.ArcLength(contour, true) * 0.08, true);
                        //仅考虑面积大于50的轮廓
                        if (CvInvoke.ContourArea(approxContour, false) > 50)
                        {
                            if (approxContour.Size == 3) //轮廓有3个顶点：三角形
                            {
                                System.Drawing.Point[] pts = approxContour.ToArray();
                                triangleList.Add(new Triangle2DF(pts[0], pts[1], pts[2]));
                            }
                            else if (approxContour.Size == 4) //轮廓有4个顶点
                            {
                                #region 检测角度，如果角度都在 [80, 100] 之间，则为矩形
                                bool isRectangle = true;
                                System.Drawing.Point[] pts = approxContour.ToArray();
                                LineSegment2D[] edges = Emgu.CV.PointCollection.PolyLine(pts, true);

                                for (int j = 0; j < edges.Length; j++)
                                {
                                    double angle = Math.Abs(edges[(j + 1) % edges.Length].GetExteriorAngleDegree(edges[j]));
                                    if (angle < 80 || angle > 100)
                                    {
                                        isRectangle = false;
                                        break;
                                    }
                                }
                                #endregion
                                if (isRectangle) boxList.Add(CvInvoke.MinAreaRect(approxContour));
                            }
                        }
                    }
                }
            }
            #endregion

          
            #region 保存剪切的最大的矩形图片  
            Rectangle rectangle = new Rectangle(0, 0, src.Width, src.Height);
            int maxWidth = 0;
            //boxList = boxList.Where(p => p.Size.Width > 300).ToList();
            for (int i = 0; i < boxList.Count(); i++)
            {
                RotatedRect box = boxList[i];
                Rectangle rectangleTemp = box.MinAreaRect();
                //这里对取到的顶点坐标进行了加宽，因为矩形可能存在角度，这里没有进行角度旋转，所以加宽了取值范围就可以取到完整的图了
                rectangleTemp = new Rectangle(rectangleTemp.X * scale, rectangleTemp.Y * scale, rectangleTemp.Width * scale + scale, rectangleTemp.Height * scale + scale);
              
                //取最大的矩形图片
                if (rectangleTemp.Width > maxWidth)
                {
                    maxWidth = rectangleTemp.Width;
                    rectangle = rectangleTemp;
                }
            }
            src.Draw(rectangle, new Bgr(System.Drawing.Color.Red), 4);//在图片中画线
            CvInvoke.Imwrite("原始图片.bmp", src); //保存原始图片
            CvInvoke.cvSetImageROI(src.Ptr, rectangle);//设置兴趣点—ROI（region of interest ）
            var clone = src.Clone(); 
            CvInvoke.Imwrite("剪切的矩形图片.bmp", clone); //保存结果图  
            #endregion
            src.Dispose();
            srcNewSize.Dispose();
            grayImage.Dispose();
        }

        #endregion

        #region 园形 
        private void btnCircle_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog frm = new System.Windows.Forms.OpenFileDialog();
            frm.Filter = "(*.jpg,*.png,*.jpeg,*.bmp,*.gif)|*.jgp;*.png;*.jpeg;*.bmp;*.gif|All files(*.*)|*.*";
            if (frm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                CutCircleImage(frm.FileName);
            }
        }
        public void CutCircleImage(string imagePath)
        { 
            Image<Bgr, Byte> src = new Image<Bgr, byte>(imagePath);
          
            int scale = 1;
            if (src.Width > 500)
            {
                scale = 2;
            }
            if (src.Width > 1000)
            {
                scale = 10;
            }
            if (src.Width > 10000)
            {
                scale = 100;
            } 
            var size = new Size(src.Width / scale, src.Height / scale);
            Image<Bgr, Byte> srcNewSize = new Image<Bgr, byte>(size);
            CvInvoke.Resize(src, srcNewSize, size);
            //将图像转换为灰度
            UMat grayImage = new UMat();
            CvInvoke.CvtColor(srcNewSize, grayImage, ColorConversion.Bgr2Gray); 
            //使用高斯滤波去除噪声
            CvInvoke.GaussianBlur(grayImage, grayImage, new Size(3, 3), 3); 
            //霍夫圆检测
            CircleF[] circles = CvInvoke.HoughCircles(grayImage, HoughModes.Gradient, 2.0, 200.0, 100.0, 180.0, 5);
          
            Rectangle rectangle = new Rectangle();
            float maxRadius = 0;
            foreach (CircleF circle in circles)
            {
                var center = circle.Center;//圆心
                var radius = circle.Radius;//半径
                if (radius > maxRadius)
                {
                    maxRadius = radius;
                    rectangle = new Rectangle((int)(center.X - radius) * scale,
                        (int)(center.Y - radius) * scale,
                        (int)radius * 2 * scale + scale,
                        (int)radius * 2 * scale + scale);
                }
                srcNewSize.Draw(circle, new Bgr(System.Drawing.Color.Blue), 4);

            }
            CvInvoke.Imwrite("原始图片.bmp", srcNewSize); //保存原始图片
            if (maxRadius == 0)
            {
                MessageBox.Show("没有圆形");
            }
            CvInvoke.cvSetImageROI(srcNewSize.Ptr, rectangle);//设置兴趣点—ROI（region of interest ）
            var clone = srcNewSize.Clone();
            CvInvoke.Imwrite("剪切的圆形图片.bmp", clone); //保存结果图  
            src.Dispose();
            srcNewSize.Dispose();
            grayImage.Dispose();
        }
        #endregion
    }
}
