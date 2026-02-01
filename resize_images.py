#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
图片缩放脚本
将4K分辨率的图片缩放到1080p（宽度和高度都缩小到原来的一半）
"""

from PIL import Image
import os

# 目标目录
target_dir = r"C:\Home\demo\BGI\BetterGenshinImpact\GameTask\Common\Element\Assets\1920x1080"

# 需要缩放的图片文件名
image_files = ["paimon_menu.png"]

def resize_image(image_path):
    """
    将图片缩放到原来的一半
    """
    try:
        # 打开图片
        img = Image.open(image_path)
        original_size = img.size
        print(f"原始尺寸: {original_size[0]}x{original_size[1]}")
        
        # 计算新尺寸（宽度和高度都除以2）
        new_width = original_size[0] // 2
        new_height = original_size[1] // 2
        new_size = (new_width, new_height)
        
        # 缩放图片（使用高质量的LANCZOS算法）
        resized_img = img.resize(new_size, Image.Resampling.LANCZOS)
        print(f"新尺寸: {new_width}x{new_height}")
        
        # 保存图片（覆盖原文件）
        resized_img.save(image_path, "PNG")
        print(f"✓ 成功缩放并保存: {image_path}")
        
        return True
    except Exception as e:
        print(f"✗ 处理失败: {image_path}")
        print(f"  错误信息: {str(e)}")
        return False

def main():
    print("=" * 60)
    print("图片缩放工具 - 将4K图片缩放到1080p")
    print("=" * 60)
    print()
    
    # 检查目录是否存在
    if not os.path.exists(target_dir):
        print(f"✗ 错误: 目录不存在: {target_dir}")
        return
    
    print(f"目标目录: {target_dir}")
    print()
    
    # 处理每个图片文件
    success_count = 0
    for filename in image_files:
        image_path = os.path.join(target_dir, filename)
        
        print(f"处理文件: {filename}")
        
        # 检查文件是否存在
        if not os.path.exists(image_path):
            print(f"✗ 文件不存在: {image_path}")
            print()
            continue
        
        # 缩放图片
        if resize_image(image_path):
            success_count += 1
        
        print()
    
    # 总结
    print("=" * 60)
    print(f"处理完成！成功: {success_count}/{len(image_files)}")
    print("=" * 60)

if __name__ == "__main__":
    main()
