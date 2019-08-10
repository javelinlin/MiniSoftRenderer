// jave.lin 2019.08.10
#define PC // 透视校正开关 - perspective correct

using RendererCoreCommon.Renderer.Common.Mathes;
using RendererCoreCommon.Renderer.Common.Shader;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiniSoftRenderer
{
    /// <summary>
    /// jave.lin 2019.08.10
    /// 重新编写一个简单的软栅格渲染器
    /// </summary>
    public partial class MainForm : Form
    {
        // 非常简单的顶点、片段着色器
        public class SimpleVS : BaseShader
        {
            public override void Main() => dc.clipPos = Mats[0] * dc.pos;
        }
        public class SimpleFS : BaseShader
        {
            public override void Main()
            {
                var texColor = sampler.Sample(Textures[0], dc.inUV);
                dc.outColor = texColor;
                return;

                var v = dc.inUV.y * 100;
                var times = (int)(v / 10);
                if (times % 2 == 0) dc.outColor = Vector4.red * dc.inColor;
                else dc.outColor = Vector4.green * dc.inColor;

                //dc.outColor = dc.inColor;
                //dc.outColor = dc.depth;
            }
        }

        private Matrix4x4 modelMat;
        private Bitmap deviceBmp;
        private Renderer renderer;
        private Vector3 rotate;
        private Camera cam;
        public MainForm()
        {
            InitializeComponent();
            deviceBmp = new Bitmap(pictureBox1.Width, pictureBox1.Height, PixelFormat.Format32bppArgb);
            renderer = new Renderer(pictureBox1.Width, pictureBox1.Height);
            renderer.Vertices.Add(new DrawContext
            {
                color = Vector4.red,
                pos = Vector4.Get(-1, 1, 1, 1),
                uv = new Vector2(0, 1)
            });
            renderer.Vertices.Add(new DrawContext
            {
                color = Vector4.yellow,
                pos = Vector4.Get(1, -1, 1, 1),
                uv = new Vector2(1, 0)
            });
            renderer.Vertices.Add(new DrawContext
            {
                color = Vector4.red,
                pos = Vector4.Get(-1, -1, 1, 1),
                uv = new Vector2(0, 0)
            });
            renderer.Vertices.Add(new DrawContext
            {
                color = Vector4.yellow,
                pos = Vector4.Get(1, 1, 1, 1),
                uv = new Vector2(1, 1)
            });
            renderer.Indices.AddRange(
                new int[]
                {
                    0, 1, 2,
                    0, 3, 1,
                });
            renderer.VertexShader = new SimpleVS();
            renderer.FragmentShader = new SimpleFS();
            renderer.FragmentShader.Textures[0] = new Texture2D("Images/tex.png");
            //renderer.Wireframe = true;
            renderer.FaceCull = false;
            cam = new Camera {
                fov = 60,
                aspect = (float)renderer.W / renderer.H,
                euler = Vector3.zero,
                translate = new Vector3(0, 0, -4),
                near = 0.3f,
                far = 1000f
            };
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            pictureBox1.Image = deviceBmp;

            rotate += new Vector3(0, 1, 0);
            //rotate = new Vector3(0, 60, 0);
            modelMat = Matrix4x4.GenTRS(Vector3.zero, rotate, Vector3.one);

            renderer.Clear();
            renderer.Draw(modelMat, cam);

            WriteTo(renderer.ColorBuffer, deviceBmp);

            pictureBox1.Image = deviceBmp;
        }
        private void WriteTo(Vector4[,] buff, Bitmap bmp)
        {
            var w = bmp.Width; var h = bmp.Height;
            var bmd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, bmp.PixelFormat);
            var ptr = bmd.Scan0;
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    var writev = buff[x, y];
                    var offset = (x + y * w) * 4;
                    Marshal.WriteByte(ptr, offset, (byte)(writev.b * 255));
                    Marshal.WriteByte(ptr, offset + 1, (byte)(writev.g * 255));
                    Marshal.WriteByte(ptr, offset + 2, (byte)(writev.r * 255));
                    Marshal.WriteByte(ptr, offset + 3, (byte)(writev.a * 255));
                }
            }
            bmp.UnlockBits(bmd);
        }
    }

    /// <summary>
    /// jave.lin 2019.08.10
    /// 编写一个非常简单的栅格化渲染器，便于理解，便于学习，便于实验
    /// </summary>
    public class Renderer
    {
        public int W { get; } public int H { get; }
        public List<DrawContext> Vertices { get; } = new List<DrawContext>();
        public List<int> Indices { get; } = new List<int>();
        public Vector4[,] ColorBuffer { get; }
        public Rectangle Viewport { get; set; }
        public BaseShader VertexShader { get; set; }
        public BaseShader FragmentShader { get; set; }
        public bool FaceCull { get; set; }
        public bool Wireframe { get; set; }
        public Camera Cam { get; set; }
        private List<Triangle> triangleList { get; } = new List<Triangle>();
        private Vector4[,] clearedColor { get; }
        public Renderer(int w, int h)
        {
            W = w; H = h;
            ColorBuffer = new Vector4[w, h];
            Viewport = new Rectangle(0, 0, w, h);
            clearedColor = new Vector4[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    clearedColor[x, y] = Vector4.gray * 0.5f;
        }
        public void Clear() => Array.Copy(clearedColor, ColorBuffer, clearedColor.Length);
        public void Draw(Matrix4x4 modelMat, Camera cam)
        {
            this.Cam = cam;

            var viewMat = 
                Matrix4x4.GenEulerMat(cam.euler.x, cam.euler.y, cam.euler.z) * 
                Matrix4x4.GenTranslateMat(cam.translate.x, cam.translate.y, cam.translate.z);
            var projMat = Matrix4x4.GenFrustum(cam.fov, cam.aspect, cam.near, cam.far);
            var mvpMat = projMat * viewMat * modelMat;

            // set built-in shader vairables：我们内置的shader变量，都可以再这输入
            VertexShader.Mats[0] = mvpMat;

            // vs transformations, 顶点着色器处理，一般就将顶点坐标变换到：clip pos
            foreach (var vertex in Vertices)
            {
                VertexShader.dc = vertex;
                VertexShader.Main();
            }

            // ndc pos : Normalized Device Coordinates position，直译为：归一化设备坐标，“设备”两字可去掉，重点就是“归一化”，所以又叫“归一化坐标”
            foreach (var vertex in Vertices)
            { // perspective divide : 透视除法，出了clipPos坐标，其他fragmentShader的输出参数，需要插值处理的，都处理透视除法
                vertex.invCamZ = 1 / vertex.clipPos.w; // jave.lin : inverse camera z，因为在proj矩阵m[4,3]==-1，然后投影矩阵相乘后，可将camera space下的顶点的z存于clipPos.w中
                vertex.ndcPos = vertex.clipPos * vertex.invCamZ;
#if PC
                vertex.inColor = vertex.color * vertex.invCamZ;
                vertex.inUV = vertex.uv * vertex.invCamZ;
#endif
            }

            // win pos：窗口坐标变换（或叫映射也行）
            foreach (var vertex in Vertices)
            {
                vertex.winPos = Vector4.Get(
                    Viewport.X + (vertex.ndcPos.x * 0.5f + 0.5f) * Viewport.Width,
                    Viewport.Y + (1 - (vertex.ndcPos.y * 0.5f + 0.5f)) * Viewport.Height,
                    vertex.clipPos.w, // cam z
                    1);
            }

            // primitive assembly：图元装配，这里只支持三角形
            triangleList.Clear();
            for (int i = 0; i < Indices.Count; i += 3)
            {
                var idx0 = Indices[i];
                var idx1 = Indices[i + 1];
                var idx2 = Indices[i + 2];
                var t = new Triangle { dc0 = Vertices[idx0], dc1 = Vertices[idx1], dc2 = Vertices[idx2] };
                triangleList.Add(t);
            }

            // clip：简单的裁剪处理，正常的裁剪需要对图元不同类型做不同处理，处理部分在裁剪视椎外的需要剪掉，然后再视椎内边缘生成对应的新的顶点
            foreach (var t in triangleList)
                if (shouldClip(t.dc0.ndcPos) || shouldClip(t.dc1.ndcPos) || shouldClip(t.dc2.ndcPos)) { t.clip = true; continue; }

            // facing-cull：面向剔除
            foreach (var t in triangleList)
                if (FaceCull && faceClip(t)) { t.clip = true; continue; }

            // rasterize and fs：栅格化+片段着色器处理
            foreach (var t in triangleList)
            {
                if (t.clip) continue;

                if (Wireframe)
                {
                    Rasterizer.DrawLine(t.dc0, t.dc1, this);
                    Rasterizer.DrawLine(t.dc1, t.dc2, this);
                    Rasterizer.DrawLine(t.dc2, t.dc0, this);
                }
                else
                {
                    Rasterizer.DrawTriangle(t, this);
                }
            }
        }
        public void ComputeDepth(DrawContext dc)
        { // 计算[0.0~1.0]范围的深度
            dc.depth = dc.clipPos.w / Cam.far;
        }
        private bool shouldClip(Vector4 pos) =>
                    ( // 这儿做简单的剔除
                    pos.x < -1 || pos.x > 1 ||
                    pos.y < -1 || pos.y > 1 ||
                    pos.z < -1 || pos.z > 1);
        private bool faceClip(Triangle t)
        {
            Vector2 p12p0 = t.dc1.winPos - t.dc0.winPos;
            Vector2 p22p0 = t.dc2.winPos - t.dc0.winPos;
            return !(p12p0.Cross(p22p0) > 0);
        }
    }
    public struct Camera { public Vector3 euler, translate; public float fov, aspect, near, far; }
    public class Triangle { public DrawContext dc0, dc1, dc2; public bool clip; }
    // 下面DrawContext中，我将各个阶段的数据都保留下来，便于理解、调试
    public class DrawContext {
        // vs input
        public Vector4 pos, color;
        public Vector2 uv;
        // vs output
        public Vector4 clipPos, ndcPos, winPos;
        // fs input
        public Vector4 inColor;
        public Vector2 inUV;
        // fs output
        public Vector4 outColor;
        public float depth;
        // perspective correct的关键
        public float invCamZ;           // camera z的倒数，用于投影校正用的，因为投影平面坐标中的顶点位置关系与原来投影前的位置不是线性关系，但是1 / camZ后就是线性的了
        // - 推导参考：
        // invCamZ的作用具体看这篇Scratchapixel 2.0的文章：Rasterization: a Practical Implementation
        // https://www.scratchapixel.com/lessons/3d-basic-rendering/rasterization-practical-implementation/perspective-correct-interpolation-vertex-attributes
        // 它的方式与这个稍微有些不一样，还加上了一个：barycentric质点坐标来求插值
        // - 使用方式参考：
        // 还有这篇：YzlCoder的博客：【SoftwareRender三部曲】三.流水线
        // https://blog.csdn.net/y1196645376/article/details/78937614
        // - 总结为：
        // 顶点从相机空间变换到投影空间后，再投影在近截面后
        // 顶点的位置之间的关系变得不是线性关系了
        // 但是顶点的除以CameraSpace下的z值后，数值就变成线性关系的了。
        // 这是再去插值即可得到正确结构
    }
    public class BaseShader {
        private const int max = 16;
        public Matrix4x4[]  Mats        { get; } = new Matrix4x4[max];
        public float[]      Floats      { get; } = new float[max];
        public Vector2[]    Vector2s    { get; } = new Vector2[max];
        public Vector3[]    Vector3s    { get; } = new Vector3[max];
        public Vector4[]    Vector4s    { get; } = new Vector4[max];
        public Texture2D[]  Textures    { get; } = new Texture2D[max];
        public DrawContext dc;
        protected Sampler2D sampler;
        public virtual void Main() { }
    }
    // 我估计独立出一个光栅化的类，尽量然Renderer类简洁，提高可读性
    public static class Rasterizer
    {
        private static List<DrawContext> dcSortListHelper = new List<DrawContext>();
        static Rasterizer()
        {
            dcSortListHelper.Add(null);
            dcSortListHelper.Add(null);
            dcSortListHelper.Add(null);
        }
        // 扫描线，使用插值来画线
        private static void ScanLine(DrawContext left, DrawContext right, int y, Renderer renderer)
        {
            var cb = renderer.ColorBuffer;
            var fs = renderer.FragmentShader;
            var dx = (int)(right.winPos.x - left.winPos.x);
            for (int i = 0; i < dx; i++)
            {
                var dc = Lerp(left, right, (float)i / dx);
#if PC
                var z = dc.winPos.z;
                dc.inColor *= z;
                dc.inUV *= z;
#endif
                renderer.FragmentShader.dc = dc;
                renderer.FragmentShader.Main();
                renderer.ColorBuffer[(int)(left.winPos.x + i), (int)left.winPos.y] = dc.outColor;
            }
        }
        // 绘制线，使用向量的方式来画线
        public static void DrawLine(DrawContext f0, DrawContext f1, Renderer renderer)
        {
            var dir = f1.winPos - f0.winPos;
            var dir_nrl = dir.normalized;
            int count = 0;
            // 如果x步幅大，就用x来遍历
            if (dir_nrl.x != 0 && (Math.Abs(dir_nrl.y) < Math.Abs(dir_nrl.x)))
            {
                dir_nrl *= Math.Abs(1 / dir_nrl.x);
                dir_nrl.x = dir_nrl.x > 0 ? 1 : -1;
                count = (int)Math.Abs(dir.x);
            }
            // 如果y步幅大，就用y来遍历
            else if (dir_nrl.y != 0)
            {
                dir_nrl *= Math.Abs(1 / dir_nrl.y);
                dir_nrl.y = dir_nrl.y > 0 ? 1 : -1;
                count = (int)Math.Abs(dir.y);
            }
            else if (dir_nrl.z != 0) throw new Exception("error");
            else throw new Exception("error");

            var cb = renderer.ColorBuffer;
            var fs = renderer.FragmentShader;
            for (int i = 0; i < count; i++)
            {
                var dc = Lerp(f0, f1, (float)i / count);
#if PC
                var z = dc.winPos.z;
                dc.inColor *= z;
                dc.inUV *= z;
#endif
                fs.dc = dc;
                fs.Main();
                cb[(int)dc.winPos.x, (int)dc.winPos.y] = dc.outColor;
            }
        }
        // dc插值
        private static DrawContext Lerp(DrawContext from, DrawContext to, float t)
        {
            var tt = 1 - t;
            var invCamZ = Mathf.Lerp(from.invCamZ, to.invCamZ, t, tt);
            var x = Mathf.Lerp(from.winPos.x, to.winPos.x, t, tt);
            var y = Mathf.Lerp(from.winPos.y, to.winPos.y, t, tt);
            var z = 1 / invCamZ;
            var result = new DrawContext
            {
                winPos = Vector4.Get(x, y, z, 1),
                inColor = Mathf.Lerp(from.inColor, to.inColor, t, tt),
                inUV = Mathf.Lerp(from.inUV, to.inUV, t, tt),
                invCamZ = invCamZ,
            };
            return result;
        }
        // 平顶三角绘制
        private static void DrawTriangleFlatTop(DrawContext left, DrawContext right, DrawContext middle, Renderer renderer)
        {
            if (left.winPos.x < right.winPos.x)
            {
                // noops
            }
            else
            {
                var t = left;
                left = right;
                right = t;
            }

            var sy = (int)left.winPos.y;
            var dy = (int)(middle.winPos.y - sy);
            for (int i = 0; i < dy; i++)
            {
                var t = (float)i / dy;
                var line_left = Lerp(left, middle, t);
                var line_right = Lerp(right, middle, t);
                ScanLine(line_left, line_right, sy + i, renderer);
            }
        }
        // 平底三角绘制
        private static void DrawTriangleFlatBottom(DrawContext left, DrawContext right, DrawContext middle, Renderer renderer)
        {
            if (left.winPos.x < right.winPos.x)
            {
                // noops
            }
            else
            {
                var t = left;
                left = right;
                right = t;
            }

            var sy = (int)middle.winPos.y;
            var dy = (int)(left.winPos.y - sy);
            for (int i = 0; i < dy; i++)
            {
                var t = (float)i / dy;
                var line_left = Lerp(left, middle, t);
                var line_right = Lerp(right, middle, t);
                ScanLine(line_left, line_right, sy + i, renderer);
            }
        }
        // 绘制三角形
        public static void DrawTriangle(Triangle triangle, Renderer renderer)
        {
            var dc0 = triangle.dc0;
            var dc1 = triangle.dc1;
            var dc2 = triangle.dc2;

            if (dc0.winPos.y == dc1.winPos.y)
            {
                if (dc2.winPos.y > dc0.winPos.y) DrawTriangleFlatTop(dc0, dc1, dc2, renderer); // 平顶三角
                else DrawTriangleFlatBottom(dc0, dc1, dc2, renderer); // 平底三角
            }
            else if (dc0.winPos.y == dc2.winPos.y)
            {
                if (dc1.winPos.y > dc0.winPos.y) DrawTriangleFlatTop(dc0, dc2, dc1, renderer); // 平顶三角
                else DrawTriangleFlatBottom(dc0, dc2, dc1, renderer); // 平底三角
            }
            else if (dc1.winPos.y == dc2.winPos.y)
            {
                if (dc0.winPos.y > dc1.winPos.y) DrawTriangleFlatTop(dc1, dc2, dc0, renderer); // 平顶三角
                else DrawTriangleFlatBottom(dc1, dc2, dc0, renderer); // 平底三角
            }
            else // 划分出两：平顶、平底
            {
                dcSortListHelper[0] = dc0;
                dcSortListHelper[1] = dc1;
                dcSortListHelper[2] = dc2;

                dcSortListHelper.Sort(compareDc);

                DrawContext topDc = dcSortListHelper[0];
                DrawContext middleDc = dcSortListHelper[1];
                DrawContext bottomDc = dcSortListHelper[2];

                var t = (middleDc.winPos.y - topDc.winPos.y) / (bottomDc.winPos.y - topDc.winPos.y);
                var parallelDc = Lerp(topDc, bottomDc, t);

                DrawTriangleFlatBottom(middleDc, parallelDc, topDc, renderer);
                DrawTriangleFlatTop(middleDc, parallelDc, bottomDc, renderer);
            }
        }
        // 片段上下文的Y左边排序
        private static int compareDc(DrawContext a, DrawContext b) => (a.winPos.y < b.winPos.y ? -1 : 1);
    }
}
