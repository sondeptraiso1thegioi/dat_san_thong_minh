# Mô hình AI biển số (đã train, dùng trực tiếp trong API .NET 10)

| File | Là gì | Nguồn |
|---|---|---|
| `model_a_yolov8n.onnx` | **Model A** – YOLOv8n 1 lớp `bien_so`, đầu vào 1×3×640×640, đầu ra 1×5×8400 | `yolo/model_a.zip` → `runs/model_a/weights/best.onnx` (train Colab 05/11/2026, Ultralytics 8.4, export opset 12) |
| `paddleocr_rec_bien_so.onnx` | **PaddleOCR** PP-OCRv5 mobile rec đã fine-tune biển VN, đọc 1 dòng, đầu vào 1×3×48×W (BGR) | `ocr/ocr_bien_so.zip` → `inference/rec_bien_so/` (Paddle 3.x) chuyển sang ONNX |
| `dict_bien_so.txt` | Từ điển 36 ký tự (0–9, A–Z bỏ W, Đ); chỉ số 0 của mô hình là ký tự trống CTC | `ocr/ocr_bien_so.zip` → `inference/dict_bien_so.txt` |

## Cách chuyển PaddleOCR sang ONNX (để làm lại khi train lại)

```bash
pip install paddlepaddle==3.2.2 paddle2onnx==2.1.0 onnxruntime   # Python 3.12
paddle2onnx --model_dir inference/rec_bien_so --model_filename inference.json \
            --params_filename inference.pdiparams --save_file paddleocr_rec_bien_so.onnx --opset_version 14
```

Đã kiểm: cùng một đầu vào, ONNX và Paddle gốc lệch tối đa 2·10⁻⁶, ký tự đọc ra trùng 100%.

## Pipeline trong code (`backend/src/Modules/AiBienSo/Infrastructure`)

1. `AnhRgb.GiaiMa`: giải mã JPEG/PNG bằng SkiaSharp (libjpeg-turbo, cho ra đúng từng điểm ảnh như OpenCV lúc train), xoay theo EXIF, chặn ảnh > 12 MP.
2. `ModelATimBien`: letterbox 640 (viền 114), RGB/255 → YOLOv8n → lọc điểm ≥ 0,4 (ngưỡng F1 tốt nhất lúc đánh giá) → NMS IoU 0,45.
3. Cắt sát hộp biển. `PaddleOcrDocDong` đọc 2 cách: cả vùng là 1 dòng, và tách đôi theo chiều cao (2 dòng). Cao 48, giữ tỉ lệ, (x/255−0,5)/0,5, giải mã CTC.
4. `QuyTacDocBienSo.ChonCachDoc`: giữ cách cho chuỗi đúng định dạng biển VN; cả hai đúng thì lấy điểm cao hơn.
5. `DocBienSoUseCase`: chỉ tự nhận khi có đúng 1 biển và điểm ≥ 0,8; còn lại trả `canXacNhan = true` kèm ứng viên.

## Kết quả đo (trên máy, CPU)

- OCR, 1.216 biển dân sự của tập val `lp_ocr_dataset_vi`, cách tách đôi giữa: **đúng cả biển 75,7 %** (lúc train báo 76,7 %). Tách theo hàng sáng nhất chỉ được 71,2 % nên không dùng.
- Cả chuỗi Model A → PaddleOCR, 20 ảnh ngẫu nhiên tập test Model A (10 ô tô, 10 xe máy): **đúng 16**, không đoán 2 (biển nghiêng / bị che, chuỗi sai định dạng), **sai 2 nhưng điểm vẫn ≥ 0,9** (51F-598.81 đọc thành 51F5981 vì CTC gộp hai số 8 liền nhau; biển xe máy bị che một phần). ~130 ms/ảnh.
- Lưu ý: tập test của Model A có ảnh gần trùng với tập train (cùng xe nhiều khung hình), nên con số này lạc quan hơn camera thật. Cần đo lại bằng video bãi trường.
