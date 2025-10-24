using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;


namespace VnDocSign.Infrastructure.Documents.Templates
{
    internal static class OpenXmlTemplateEngine
    {
        /// <summary>
        /// Ghi text vào RichText/Picture ContentControl theo alias hoặc tag.
        /// </summary>
        public static void FillRichTextByAlias(WordprocessingDocument doc, string alias, string? text)
        {
            foreach (var sdt in doc.MainDocumentPart!.Document.Descendants<SdtElement>())
            {
                var props = sdt.SdtProperties;
                var tag = props?.GetFirstChild<Tag>()?.Val?.Value;
                var aliasNode = props?.GetFirstChild<SdtAlias>()?.Val?.Value;


                if (!alias.Equals(aliasNode, StringComparison.OrdinalIgnoreCase)
                && !alias.Equals(tag, StringComparison.OrdinalIgnoreCase))
                    continue;


                // Lấy container cho nội dung
                OpenXmlElement? content = sdt.GetFirstChild<SdtContentBlock>()
                ?? (OpenXmlElement?)sdt.GetFirstChild<SdtContentRun>();
                content ??= sdt; // fallback


                var runTexts = content.Descendants<Text>().ToList();
                if (runTexts.Count == 0)
                {
                    var para = new Paragraph(new Run(new Text(text ?? string.Empty)));
                    content.RemoveAllChildren();
                    content.AppendChild(para);
                }
                else
                {
                    // Xoá toàn bộ rồi append 1 run gọn gàng (tránh rác run)
                    var parent = runTexts[0].Parent; // để giữ style cơ bản nếu cần
                    var container = content as OpenXmlCompositeElement ?? sdt;
                    container.RemoveAllChildren<Paragraph>();
                    container.AppendChild(new Paragraph(new Run(new Text(text ?? string.Empty))));
                }
            }
        }


        /// <summary>
        /// Xoá toàn bộ block (SDT) theo alias/tag. Dùng cho BLOCK_* ẩn/hiện.
        /// </summary>
        public static void RemoveBlockByAlias(WordprocessingDocument doc, string alias)
        {
            foreach (var sdt in doc.MainDocumentPart!.Document.Descendants<SdtElement>().ToList())
            {
                var props = sdt.SdtProperties;
                var tag = props?.GetFirstChild<Tag>()?.Val?.Value;
                var aliasNode = props?.GetFirstChild<SdtAlias>()?.Val?.Value;


                if (alias.Equals(aliasNode, StringComparison.OrdinalIgnoreCase)
                || alias.Equals(tag, StringComparison.OrdinalIgnoreCase))
                {
                    sdt.Remove();
                }
            }
        }


        /// <summary>
        /// Đặt ảnh PNG vào Picture ContentControl theo alias/tag. Kích thước ~ 6cm x 4cm.
        /// </summary>
        public static void SetImageByAlias(WordprocessingDocument doc, string alias, byte[] pngContent)
        {
            foreach (var sdt in doc.MainDocumentPart!.Document.Descendants<SdtElement>())
            {
                var props = sdt.SdtProperties;
                var tag = props?.GetFirstChild<Tag>()?.Val?.Value;
                var aliasNode = props?.GetFirstChild<SdtAlias>()?.Val?.Value;


                if (!alias.Equals(aliasNode, StringComparison.OrdinalIgnoreCase)
                && !alias.Equals(tag, StringComparison.OrdinalIgnoreCase))
                    continue;


                // Clear drawings cũ
                foreach (var d in sdt.Descendants<DocumentFormat.OpenXml.Wordprocessing.Drawing>().ToList()) d.Remove();


                // Add image part
                var main = doc.MainDocumentPart!;
                var imgPart = main.AddImagePart(ImagePartType.Png);
                using (var ms = new MemoryStream(pngContent)) imgPart.FeedData(ms);
                var relId = main.GetIdOfPart(imgPart);


                var drawing = BuildImageDrawing(relId);


                // Chèn vào content
                var content = sdt.GetFirstChild<SdtContentBlock>()
                ?? (OpenXmlElement?)sdt.GetFirstChild<SdtContentRun>()
                ?? sdt;
                var para = new Paragraph(new Run(drawing));
                content.RemoveAllChildren<Paragraph>();
                content.AppendChild(para);
            }
        }
        private static DocumentFormat.OpenXml.Wordprocessing.Drawing BuildImageDrawing(string relId)
        {
            // 6cm x 4cm ~ 6000000 x 4000000 EMU
            const long cx = 6000000;
            const long cy = 4000000;


            return new DocumentFormat.OpenXml.Wordprocessing.Drawing(
            new DW.Inline(
            new DW.Extent() { Cx = cx, Cy = cy },
            new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties() { Id = 1U, Name = "SignatureImage" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }),
            new A.Graphic(
            new A.GraphicData(
            new PIC.Picture(
            new PIC.NonVisualPictureProperties(
            new PIC.NonVisualDrawingProperties() { Id = 0U, Name = "sig.png" },
            new PIC.NonVisualPictureDrawingProperties()
            ),
            new PIC.BlipFill(
            new A.Blip() { Embed = relId },
            new A.Stretch(new A.FillRectangle())
            ),
            new PIC.ShapeProperties(
            new A.Transform2D(
            new A.Offset() { X = 0L, Y = 0L },
            new A.Extents() { Cx = cx, Cy = cy }
            ),
            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }
            )
            )
            )
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
            )
            )
            );
        }
    }
}