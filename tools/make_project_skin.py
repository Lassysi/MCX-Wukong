from PIL import Image

# ------------------------------------------------------------------ 调色板
FUR_D = (74, 52, 32)      # 深猴毛
FUR_M = (110, 78, 48)     # 中猴毛
FUR_L = (140, 104, 68)    # 浅猴毛
FACE = (214, 176, 136)    # 脸/手（浅栗色）
FACE_D = (176, 138, 104)  # 脸部阴影
EYE = (28, 20, 16)        # 眼睛
GOLD = (196, 150, 42)     # 金
GOLD_L = (232, 196, 92)   # 亮金
GOLD_D = (138, 102, 26)   # 暗金
RED = (122, 30, 30)       # 暗红
RED_L = (166, 52, 44)     # 亮红
CLOTH = (34, 34, 44)      # 深色内衬
BOOT = (58, 42, 26)       # 靴
OUTLINE = (40, 28, 18)    # 描边


def rect(img, x, y, w, h, color):
    for j in range(h):
        for i in range(w):
            img.putpixel((x + i, y + j), color)


def row(img, x, y, colors):
    for i, c in enumerate(colors):
        if c is not None:
            img.putpixel((x + i, y), c)


# ------------------------------------------------------------------ 皮肤绘制
def build_skin():
    img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))

    # ---------------- 头 ----------------
    def head_face(dx, dy, front):
        rect(img, dx, dy, 8, 8, FUR_M)
        # 金箍：贯穿整圈（第 2 行），正面中间两颗亮金珠
        rect(img, dx, dy + 2, 8, 1, GOLD)
        rect(img, dx, dy + 1, 8, 1, GOLD_D)
        if front:
            rect(img, dx + 3, dy + 2, 2, 1, GOLD_L)
            # 猴脸：尽可能占满正面中部
            row(img, dx, dy + 3, [FUR_M, FACE, FACE, FACE, FACE, FACE, FACE, FUR_M])
            row(img, dx, dy + 4, [FUR_M, FACE, EYE, FACE, FACE, EYE, FACE, FUR_M])
            row(img, dx, dy + 5, [FUR_M, FACE, FACE, FACE_D, FACE_D, FACE, FACE, FUR_M])
            row(img, dx, dy + 6, [FUR_M, FACE, FACE_D, FACE_D, FACE_D, FACE_D, FACE, FUR_M])
            row(img, dx, dy + 7, [FUR_M, FUR_M, FACE, FACE, FACE, FACE, FUR_M, FUR_M])
        else:
            # 侧面/背面：猴毛 + 一点脸部延伸，避免看起来是光板
            rect(img, dx, dy + 3, 8, 5, FUR_M)
            rect(img, dx + 1, dy + 4, 2, 3, FUR_L)
            rect(img, dx + 5, dy + 5, 2, 2, FUR_D)

    head_face(8, 8, True)     # front
    head_face(0, 8, False)    # right
    head_face(16, 8, False)   # left
    head_face(24, 8, False)   # back
    rect(img, 8, 0, 8, 8, FUR_M)      # top
    rect(img, 8, 0, 8, 1, GOLD_D)
    rect(img, 16, 0, 8, 8, FUR_D)     # bottom

    # ---------------- 身体 ----------------
    def body_face(dx, dy, front):
        rect(img, dx, dy, 8, 12, GOLD)
        rect(img, dx, dy, 8, 2, GOLD_L)          # 肩甲
        rect(img, dx, dy + 8, 8, 1, GOLD_D)      # 腰带
        if front:
            rect(img, dx + 3, dy + 2, 2, 6, RED)     # 胸前红带
            rect(img, dx + 2, dy + 3, 4, 1, RED_L)
            rect(img, dx + 1, dy + 9, 6, 3, RED)     # 下摆
        else:
            rect(img, dx + 1, dy + 2, 6, 6, RED)     # 披风
            rect(img, dx + 1, dy + 9, 6, 3, RED)
        rect(img, dx, dy + 9, 8, 3, RED if not front else RED)
    body_face(20, 20, True)
    body_face(32, 20, False)
    rect(img, 16, 20, 4, 12, GOLD_D)    # right side
    rect(img, 28, 20, 4, 12, GOLD_D)    # left side
    rect(img, 20, 16, 8, 4, GOLD_L)     # top
    rect(img, 28, 16, 8, 4, GOLD_D)     # bottom

    # ---------------- 手臂 ----------------
    def arm_faces(dx_front, dx_right, dx_left, dx_back, dx_top, dx_bottom):
        rect(img, dx_front, 20, 4, 12, CLOTH)
        rect(img, dx_front, 20, 4, 4, GOLD)      # 肩甲
        rect(img, dx_front, 30, 4, 2, FACE)      # 手
        for d in (dx_right, dx_left, dx_back):
            rect(img, d, 20, 4, 12, CLOTH)
            rect(img, d, 20, 4, 4, GOLD)
            rect(img, d, 30, 4, 2, FACE_D)
        rect(img, dx_top, 16, 4, 4, GOLD_L)
        rect(img, dx_bottom, 16, 4, 4, FACE_D)

    arm_faces(44, 40, 48, 52, 44, 48)          # 右臂
    arm_faces(36, 32, 40, 44, 36, 40)          # 左臂（base）

    # ---------------- 腿 ----------------
    def leg_faces(dx_front, dx_right, dx_left, dx_back, dx_top, dx_bottom):
        for d in (dx_front, dx_right, dx_left, dx_back):
            rect(img, d, 20, 4, 12, RED)
            rect(img, d, 27, 4, 5, BOOT)         # 靴
            rect(img, d, 26, 4, 1, GOLD_D)       # 靴口
        rect(img, dx_top, 16, 4, 4, RED)
        rect(img, dx_bottom, 16, 4, 4, BOOT)

    leg_faces(4, 0, 8, 12, 4, 8)               # 右腿
    leg_faces(20, 16, 24, 28, 20, 24)          # 左腿

    # ---------------- 袖子（手臂覆盖层，mesh 里确实有）----------------
    for (dx, dy) in ((40, 32), (48, 32), (48, 48), (40, 48)):
        rect(img, dx, dy + 4, 4, 8, GOLD_D)

    # 描边：把 alpha=0 且与不透明相邻的像素留空即可，这里不做处理
    return img


# ------------------------------------------------------------------ 重烘 mesh
if __name__ == "__main__":
    import argparse
    from pathlib import Path
    parser=argparse.ArgumentParser(description="Generate the project's original 64x64 skin")
    parser.add_argument("output")
    args=parser.parse_args()
    Path(args.output).parent.mkdir(parents=True,exist_ok=True)
    build_skin().save(args.output)
