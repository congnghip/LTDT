using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Sang6_WebAPI.Services
{
    public class FaceVerificationResult
    {
        public bool Success { get; set; }
        public bool IsMatch { get; set; }
        public double SimilarityPercentage { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool HasRegisteredFace { get; set; }
        public string? RegisteredFaceBase64 { get; set; }
    }

    public static class FaceRecognitionService
    {
        public const double MatchThreshold = 66.5;

        public static byte[]? CleanBase64(string base64)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(base64)) return null;
                var clean = base64;
                var commaIdx = clean.IndexOf(',');
                if (commaIdx >= 0)
                {
                    clean = clean.Substring(commaIdx + 1);
                }
                return Convert.FromBase64String(clean.Trim());
            }
            catch
            {
                return null;
            }
        }

        public static FaceVerificationResult VerifyFace(
            string? registeredBase64,
            string capturedBase64,
            string employeeName,
            List<double>? capturedRatios = null)
        {
            var capturedBytes = CleanBase64(capturedBase64);
            if (capturedBytes == null || capturedBytes.Length < 100)
            {
                return new FaceVerificationResult
                {
                    Success = false,
                    IsMatch = false,
                    SimilarityPercentage = 0,
                    Message = "Ảnh chụp từ Camera không hợp lệ hoặc dữ liệu bị lỗi."
                };
            }

            if (string.IsNullOrWhiteSpace(registeredBase64))
            {
                return new FaceVerificationResult
                {
                    Success = true,
                    IsMatch = true,
                    HasRegisteredFace = false,
                    SimilarityPercentage = 100.0,
                    Message = $"Nhân viên {employeeName} chưa có khuôn mặt gốc. Hệ thống sẽ tự động đăng ký ảnh khuôn mặt chuẩn!"
                };
            }

            var registeredBytes = CleanBase64(registeredBase64);
            if (registeredBytes == null || registeredBytes.Length < 100)
            {
                return new FaceVerificationResult
                {
                    Success = true,
                    IsMatch = true,
                    HasRegisteredFace = false,
                    SimilarityPercentage = 100.0,
                    Message = "Khuôn mặt gốc đang trống, sẽ cập nhật ảnh mới."
                };
            }

            try
            {
                using var rawRegImg = Image.Load<Rgba32>(registeredBytes);
                using var rawCapImg = Image.Load<Rgba32>(capturedBytes);

                // 1. Kiểm tra dải màu da người cơ bản
                bool hasHumanSkin = CheckSkinToneRatio(rawCapImg);
                if (!hasHumanSkin)
                {
                    return new FaceVerificationResult
                    {
                        Success = false,
                        IsMatch = false,
                        HasRegisteredFace = true,
                        SimilarityPercentage = 18.5,
                        Message = "CẢNH BÁO: Không phát hiện khuôn mặt người trong ảnh! Vui lòng không chụp vật thể, trần nhà hoặc màn hình giả mạo."
                    };
                }

                // 2. Chuẩn hóa & Cắt khuôn mặt
                const int W = 128;
                const int H = 128;
                using var regFace = EnsureFaceCroppedAndNormalized(rawRegImg, W, H);
                using var capFace = EnsureFaceCroppedAndNormalized(rawCapImg, W, H);

                // 3. Cân bằng ánh sáng (Histogram Equalization / Contrast Stretching)
                NormalizeLighting(regFace);
                NormalizeLighting(capFace);

                // 4. Tìm độ dịch chuyển tối ưu (Best-shift alignment dx, dy) giữa 2 khuôn mặt
                var (bestDx, bestDy, alignCorr) = FindBestAlignmentOffset(regFace, capFace, maxShift: 14);

                // 5. Tạo ảnh capFace đã căn chỉnh theo (bestDx, bestDy)
                using var alignedCapFace = CreateShiftedImage(capFace, bestDx, bestDy);

                // 6. Phân tích đa vùng sinh trắc học đã căn chỉnh (Multi-Zone Facial Biometrics)
                // Tập trung cao độ vào mắt (30-55%) và sống mũi/miệng
                double browSim = CompareZone(regFace, alignedCapFace, 0.15, 0.35);
                double eyeSim = CompareZone(regFace, alignedCapFace, 0.30, 0.55);
                double noseSim = CompareZone(regFace, alignedCapFace, 0.50, 0.72);
                double mouthSim = CompareZone(regFace, alignedCapFace, 0.70, 0.92);

                // 7. So sánh viền cạnh Sobel dung sai vị trí (Tolerant Edge Match)
                double edgeSim = CompareTolerantSobelEdge(regFace, alignedCapFace, toleranceRadius: 2);

                // 8. So sánh đặc trưng kết cấu cục bộ LBP (Local Binary Pattern) chuẩn hóa
                double lbpSim = CompareLbpHistograms(regFace, alignedCapFace, gridX: 6, gridY: 6);

                // 9. So sánh độ chênh lệch cường độ chi tiết (Normalized Pixel Difference)
                double pixelDiffSim = ComparePixelL1(regFace, alignedCapFace);

                // 10. Perceptual dHash
                double hashSim = CompareAlignedDHash(regFace, alignedCapFace);

                // 11. Tỷ lệ hình học
                double ratioSim = 0.85;
                if (capturedRatios != null && capturedRatios.Count >= 3)
                {
                    ratioSim = EvaluateBiometricRatios(capturedRatios);
                }

                // Điểm tổng hợp cân bằng:
                // Eye & Nose/Mouth Zones (35%) + LBP Texture (25%) + Tolerant Edge (18%) + Pixel Diff (12%) + Hash (10%)
                double zoneScore = (eyeSim * 0.40) + (mouthSim * 0.25) + (noseSim * 0.20) + (browSim * 0.15);

                double weightedScore = (zoneScore * 0.35) +
                                       (lbpSim * 0.25) +
                                       (edgeSim * 0.18) +
                                       (pixelDiffSim * 0.12) +
                                       (hashSim * 0.10);

                Console.WriteLine($"[FaceBio] dx={bestDx}, dy={bestDy}, alignCorr={alignCorr:F3}, lbp={lbpSim:F3}, zone={zoneScore:F3} (eye={eyeSim:F3}, mouth={mouthSim:F3}), edge={edgeSim:F3}, pixDiff={pixelDiffSim:F3}, hash={hashSim:F3}, score={weightedScore:F3}");

                // Xử lý bảo mật chống mạo danh:
                // Nếu mắt hoặc cấu trúc viền quá khác biệt (2 người khác nhau), hạ điểm mạnh mẽ
                if (eyeSim < 0.65)
                {
                    double eyeFactor = Math.Clamp(eyeSim / 0.65, 0.55, 1.0);
                    weightedScore *= eyeFactor;
                }
                if (edgeSim < 0.45)
                {
                    double edgeFactor = Math.Clamp(edgeSim / 0.45, 0.60, 1.0);
                    weightedScore *= edgeFactor;
                }
                if (lbpSim < 0.55)
                {
                    double lbpFactor = Math.Clamp(lbpSim / 0.55, 0.65, 1.0);
                    weightedScore *= lbpFactor;
                }

                double finalPercentage = Math.Round(Math.Clamp(weightedScore * 100.0, 15.0, 99.2), 1);
                bool isMatch = finalPercentage >= MatchThreshold;

                if (isMatch)
                {
                    return new FaceVerificationResult
                    {
                        Success = true,
                        IsMatch = true,
                        HasRegisteredFace = true,
                        SimilarityPercentage = finalPercentage,
                        Message = $"Xác thực khuôn mặt THÀNH CÔNG! Độ tương đồng sinh trắc học đạt {finalPercentage}% (Vượt ngưỡng an toàn {MatchThreshold}%). Đúng nhân viên {employeeName}."
                    };
                }
                else
                {
                    return new FaceVerificationResult
                    {
                        Success = false,
                        IsMatch = false,
                        HasRegisteredFace = true,
                        SimilarityPercentage = finalPercentage,
                        Message = $"TỪ CHỐI XÁC THỰC! Khuôn mặt không khớp với hồ sơ của nhân viên {employeeName}. Độ tương đồng chỉ đạt {finalPercentage}% (Dưới ngưỡng an toàn {MatchThreshold}%). Phát hiện nghi vấn giả mạo hoặc 2 người khác nhau!"
                    };
                }
            }
            catch (Exception ex)
            {
                return new FaceVerificationResult
                {
                    Success = false,
                    IsMatch = false,
                    SimilarityPercentage = 0,
                    Message = $"Lỗi xử lý sinh trắc học: {ex.Message}"
                };
            }
        }

        private static void NormalizeLighting(Image<Rgba32> img)
        {
            // Kéo giãn độ tương phản để triệt tiêu ảnh hưởng của phòng tối / phòng sáng
            byte min = 255;
            byte max = 0;
            for (int y = 0; y < img.Height; y++)
            {
                for (int x = 0; x < img.Width; x++)
                {
                    byte v = img[x, y].R;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }

            int range = max - min;
            if (range <= 10) return;

            for (int y = 0; y < img.Height; y++)
            {
                for (int x = 0; x < img.Width; x++)
                {
                    int v = ((img[x, y].R - min) * 255) / range;
                    byte b = (byte)Math.Clamp(v, 0, 255);
                    img[x, y] = new Rgba32(b, b, b, 255);
                }
            }
        }

        private static Image<Rgba32> EnsureFaceCroppedAndNormalized(Image<Rgba32> src, int targetW, int targetH)
        {
            double aspect = (double)src.Width / src.Height;
            if (aspect >= 0.8 && aspect <= 1.25)
            {
                return src.Clone(ctx => ctx.Resize(targetW, targetH).Grayscale());
            }

            int cropW = (int)(src.Width * 0.75);
            int cropH = (int)(src.Height * 0.65);
            int cropX = (src.Width - cropW) / 2;
            int cropY = (int)(src.Height * 0.15);

            cropX = Math.Clamp(cropX, 0, src.Width - 1);
            cropY = Math.Clamp(cropY, 0, src.Height - 1);
            cropW = Math.Clamp(cropW, 1, src.Width - cropX);
            cropH = Math.Clamp(cropH, 1, src.Height - cropY);

            return src.Clone(ctx => ctx
                .Crop(new Rectangle(cropX, cropY, cropW, cropH))
                .Resize(targetW, targetH)
                .Grayscale());
        }

        private static (int bestDx, int bestDy, double bestCorr) FindBestAlignmentOffset(Image<Rgba32> img1, Image<Rgba32> img2, int maxShift)
        {
            int bestDx = 0;
            int bestDy = 0;
            double maxCorr = -1.0;

            using var small1 = img1.Clone(ctx => ctx.Resize(32, 32));
            using var small2 = img2.Clone(ctx => ctx.Resize(32, 32));

            int scaledMaxShift = Math.Max(2, (maxShift * 32) / img1.Width);

            // Chỉ tập trung so sánh vùng mắt & mũi trung tâm (y từ 25% đến 75%) để tìm góc lệch
            int yMin = 8;
            int yMax = 24;

            double sum1 = 0;
            int cnt1 = 0;
            for (int y = yMin; y < yMax; y++)
            {
                for (int x = 4; x < 28; x++)
                {
                    sum1 += small1[x, y].R;
                    cnt1++;
                }
            }
            double mean1 = cnt1 > 0 ? sum1 / cnt1 : 128.0;

            for (int sdy = -scaledMaxShift; sdy <= scaledMaxShift; sdy += 1)
            {
                for (int sdx = -scaledMaxShift; sdx <= scaledMaxShift; sdx += 1)
                {
                    double sum2 = 0;
                    int count = 0;
                    for (int y = yMin; y < yMax; y++)
                    {
                        int y2 = y + sdy;
                        if (y2 < 0 || y2 >= 32) continue;
                        for (int x = 4; x < 28; x++)
                        {
                            int x2 = x + sdx;
                            if (x2 < 0 || x2 >= 32) continue;
                            sum2 += small2[x2, y2].R;
                            count++;
                        }
                    }
                    if (count < 80) continue;
                    double mean2 = sum2 / count;

                    double dot = 0;
                    double norm1 = 0;
                    double norm2 = 0;
                    for (int y = yMin; y < yMax; y++)
                    {
                        int y2 = y + sdy;
                        if (y2 < 0 || y2 >= 32) continue;
                        for (int x = 4; x < 28; x++)
                        {
                            int x2 = x + sdx;
                            if (x2 < 0 || x2 >= 32) continue;
                            double v1 = small1[x, y].R - mean1;
                            double v2 = small2[x2, y2].R - mean2;
                            dot += v1 * v2;
                            norm1 += v1 * v1;
                            norm2 += v2 * v2;
                        }
                    }

                    if (norm1 > 0.001 && norm2 > 0.001)
                    {
                        double corr = dot / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
                        if (corr > maxCorr)
                        {
                            maxCorr = corr;
                            bestDx = (sdx * img1.Width) / 32;
                            bestDy = (sdy * img1.Height) / 32;
                        }
                    }
                }
            }

            bestDx = Math.Clamp(bestDx, -maxShift, maxShift);
            bestDy = Math.Clamp(bestDy, -maxShift, maxShift);
            return (bestDx, bestDy, Math.Clamp((maxCorr + 1.0) / 2.0, 0.0, 1.0));
        }

        private static Image<Rgba32> CreateShiftedImage(Image<Rgba32> src, int dx, int dy)
        {
            var result = new Image<Rgba32>(src.Width, src.Height);
            for (int y = 0; y < src.Height; y++)
            {
                int srcY = y + dy;
                for (int x = 0; x < src.Width; x++)
                {
                    int srcX = x + dx;
                    if (srcX >= 0 && srcX < src.Width && srcY >= 0 && srcY < src.Height)
                    {
                        result[x, y] = src[srcX, srcY];
                    }
                    else
                    {
                        int clampedX = Math.Clamp(srcX, 0, src.Width - 1);
                        int clampedY = Math.Clamp(srcY, 0, src.Height - 1);
                        result[x, y] = src[clampedX, clampedY];
                    }
                }
            }
            return result;
        }

        private static double CompareZone(Image<Rgba32> img1, Image<Rgba32> img2, double startYRatio, double endYRatio)
        {
            int startY = (int)(img1.Height * startYRatio);
            int endY = (int)(img1.Height * endYRatio);

            var hash1 = ComputeZoneDHash(img1, startY, endY);
            var hash2 = ComputeZoneDHash(img2, startY, endY);
            double hashSim = CalculateDHashSimilarity(hash1, hash2);

            double corr = CalculateZoneCorrelation(img1, img2, startY, endY);

            return (hashSim * 0.35) + (corr * 0.65);
        }

        private static bool[] ComputeZoneDHash(Image<Rgba32> src, int startY, int endY)
        {
            int h = Math.Max(2, endY - startY);
            int w = src.Width;
            int sampleW = 16;
            int sampleH = 8;

            var hash = new bool[(sampleW - 1) * sampleH];
            int idx = 0;

            for (int sy = 0; sy < sampleH; sy++)
            {
                int py = startY + (sy * h / sampleH);
                py = Math.Clamp(py, 0, src.Height - 1);

                for (int sx = 0; sx < sampleW - 1; sx++)
                {
                    int px1 = sx * w / sampleW;
                    int px2 = (sx + 1) * w / sampleW;
                    px1 = Math.Clamp(px1, 0, src.Width - 1);
                    px2 = Math.Clamp(px2, 0, src.Width - 1);

                    byte b1 = src[px1, py].R;
                    byte b2 = src[px2, py].R;
                    hash[idx++] = b1 > b2;
                }
            }
            return hash;
        }

        private static double CalculateDHashSimilarity(bool[] h1, bool[] h2)
        {
            int diff = 0;
            int len = Math.Min(h1.Length, h2.Length);
            for (int i = 0; i < len; i++)
            {
                if (h1[i] != h2[i]) diff++;
            }
            return 1.0 - ((double)diff / len);
        }

        private static double CalculateZoneCorrelation(Image<Rgba32> img1, Image<Rgba32> img2, int startY, int endY)
        {
            int w = img1.Width;

            double sum1 = 0, sum2 = 0;
            int count = 0;
            for (int y = startY; y < endY; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    sum1 += img1[x, y].R;
                    sum2 += img2[x, y].R;
                    count++;
                }
            }
            double mean1 = count > 0 ? sum1 / count : 128.0;
            double mean2 = count > 0 ? sum2 / count : 128.0;

            double dot = 0.0;
            double norm1 = 0.0;
            double norm2 = 0.0;

            for (int y = startY; y < endY; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double v1 = img1[x, y].R - mean1;
                    double v2 = img2[x, y].R - mean2;
                    dot += v1 * v2;
                    norm1 += v1 * v1;
                    norm2 += v2 * v2;
                }
            }

            if (norm1 <= 0.001 || norm2 <= 0.001) return 0.5;
            double corr = dot / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
            return Math.Clamp((corr + 1.0) / 2.0, 0.0, 1.0);
        }

        private static double CompareTolerantSobelEdge(Image<Rgba32> img1, Image<Rgba32> img2, int toleranceRadius)
        {
            int w = img1.Width;
            int h = img1.Height;

            bool[,] edges1 = new bool[w, h];
            bool[,] edges2 = new bool[w, h];
            bool[,] dilatedEdges2 = new bool[w, h];

            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    int gx1 = img1[x + 1, y].R - img1[x - 1, y].R;
                    int gy1 = img1[x, y + 1].R - img1[x, y - 1].R;
                    edges1[x, y] = (Math.Abs(gx1) + Math.Abs(gy1)) > 28;

                    int gx2 = img2[x + 1, y].R - img2[x - 1, y].R;
                    int gy2 = img2[x, y + 1].R - img2[x, y - 1].R;
                    edges2[x, y] = (Math.Abs(gx2) + Math.Abs(gy2)) > 28;
                }
            }

            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    if (edges2[x, y])
                    {
                        for (int dy = -toleranceRadius; dy <= toleranceRadius; dy++)
                        {
                            int ny = y + dy;
                            if (ny < 0 || ny >= h) continue;
                            for (int dx = -toleranceRadius; dx <= toleranceRadius; dx++)
                            {
                                int nx = x + dx;
                                if (nx < 0 || nx >= w) continue;
                                dilatedEdges2[nx, ny] = true;
                            }
                        }
                    }
                }
            }

            int matched = 0;
            int total1 = 0;
            for (int y = 4; y < h - 4; y++)
            {
                for (int x = 4; x < w - 4; x++)
                {
                    if (edges1[x, y])
                    {
                        total1++;
                        if (dilatedEdges2[x, y]) matched++;
                    }
                }
            }

            if (total1 == 0) return 0.5;
            return (double)matched / total1;
        }

        private static double CompareLbpHistograms(Image<Rgba32> img1, Image<Rgba32> img2, int gridX, int gridY)
        {
            int w = img1.Width;
            int h = img1.Height;
            int cellW = w / gridX;
            int cellH = h / gridY;

            double totalSim = 0.0;
            double totalWeight = 0.0;

            for (int gy = 0; gy < gridY; gy++)
            {
                int yStart = gy * cellH;
                int yEnd = Math.Min(h, (gy + 1) * cellH);

                for (int gx = 0; gx < gridX; gx++)
                {
                    int xStart = gx * cellW;
                    int xEnd = Math.Min(w, (gx + 1) * cellW);

                    // Trọng số ưu tiên vùng mắt (gy 1,2), mũi (gy 3), miệng (gy 4)
                    double weight = 1.0;
                    if (gy >= 1 && gy <= 4 && gx >= 1 && gx <= 4)
                    {
                        weight = 1.8;
                    }

                    int[] hist1 = new int[256];
                    int[] hist2 = new int[256];
                    int count = 0;

                    for (int y = yStart + 1; y < yEnd - 1; y++)
                    {
                        for (int x = xStart + 1; x < xEnd - 1; x++)
                        {
                            byte p1 = img1[x, y].R;
                            byte p2 = img2[x, y].R;

                            int code1 = 0;
                            if (img1[x - 1, y - 1].R >= p1) code1 |= 1;
                            if (img1[x, y - 1].R >= p1) code1 |= 2;
                            if (img1[x + 1, y - 1].R >= p1) code1 |= 4;
                            if (img1[x + 1, y].R >= p1) code1 |= 8;
                            if (img1[x + 1, y + 1].R >= p1) code1 |= 16;
                            if (img1[x, y + 1].R >= p1) code1 |= 32;
                            if (img1[x - 1, y + 1].R >= p1) code1 |= 64;
                            if (img1[x - 1, y].R >= p1) code1 |= 128;
                            hist1[code1]++;

                            int code2 = 0;
                            if (img2[x - 1, y - 1].R >= p2) code2 |= 1;
                            if (img2[x, y - 1].R >= p2) code2 |= 2;
                            if (img2[x + 1, y - 1].R >= p2) code2 |= 4;
                            if (img2[x + 1, y].R >= p2) code2 |= 8;
                            if (img2[x + 1, y + 1].R >= p2) code2 |= 16;
                            if (img2[x, y + 1].R >= p2) code2 |= 32;
                            if (img2[x - 1, y + 1].R >= p2) code2 |= 64;
                            if (img2[x - 1, y].R >= p2) code2 |= 128;
                            hist2[code2]++;

                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        int intersection = 0;
                        for (int b = 0; b < 256; b++)
                        {
                            intersection += Math.Min(hist1[b], hist2[b]);
                        }
                        double cellSim = (double)intersection / count;
                        totalSim += cellSim * weight;
                        totalWeight += weight;
                    }
                }
            }

            return totalWeight > 0 ? (totalSim / totalWeight) : 0.5;
        }

        private static double ComparePixelL1(Image<Rgba32> img1, Image<Rgba32> img2)
        {
            // So sánh độ chênh lệch L1 trên vùng trung tâm khuôn mặt (x từ 20 đến 108, y từ 20 đến 108)
            int w = img1.Width;
            int h = img1.Height;
            long diffSum = 0;
            int count = 0;

            for (int y = 20; y < h - 20; y += 2)
            {
                for (int x = 20; x < w - 20; x += 2)
                {
                    diffSum += Math.Abs(img1[x, y].R - img2[x, y].R);
                    count++;
                }
            }

            if (count == 0) return 0.5;
            double avgDiff = (double)diffSum / count; // 0..255
            return Math.Clamp(1.0 - (avgDiff / 100.0), 0.0, 1.0);
        }

        private static double CompareAlignedDHash(Image<Rgba32> img1, Image<Rgba32> img2)
        {
            using var s1 = img1.Clone(ctx => ctx.Resize(17, 16));
            using var s2 = img2.Clone(ctx => ctx.Resize(17, 16));

            int diff = 0;
            int total = 16 * 16;

            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    bool b1 = s1[x, y].R > s1[x + 1, y].R;
                    bool b2 = s2[x, y].R > s2[x + 1, y].R;
                    if (b1 != b2) diff++;
                }
            }

            return 1.0 - ((double)diff / total);
        }

        private static double EvaluateBiometricRatios(List<double> capturedRatios)
        {
            if (capturedRatios.Count < 3) return 0.85;

            double eyeNose = capturedRatios[0];
            double mouthWidth = capturedRatios[1];
            double noseMouth = capturedRatios[2];

            bool inNormalHumanRange = (eyeNose >= 0.6 && eyeNose <= 1.6) &&
                                      (mouthWidth >= 0.5 && mouthWidth <= 1.5) &&
                                      (noseMouth >= 0.3 && noseMouth <= 1.2);

            return inNormalHumanRange ? 0.92 : 0.45;
        }

        private static bool CheckSkinToneRatio(Image<Rgba32> img)
        {
            int skinCount = 0;
            int totalSamples = 1000;
            var rand = new Random(42);

            for (int i = 0; i < totalSamples; i++)
            {
                int x = rand.Next(0, img.Width);
                int y = rand.Next(0, img.Height);
                var p = img[x, y];

                if (p.R > 75 && p.G > 35 && p.B > 20 && p.R > p.G && p.R > p.B && (p.R - p.G) > 10)
                {
                    skinCount++;
                }
            }

            double ratio = (double)skinCount / totalSamples;
            return ratio >= 0.08;
        }
    }
}
