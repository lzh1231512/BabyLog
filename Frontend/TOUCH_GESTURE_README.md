# 触摸手势管理器使用说明

## 概述

`TouchGestureManager` 是一个用于处理移动端触摸手势的工具类，支持以下功能：

- 单指滑动（上下左右）
- 双指缩放（pinch-to-zoom）
- 单指拖拽（在缩放状态下）
- 双击缩放
- 单击检测

## 安装和导入

```javascript
import { TouchGestureManager, createPhotoViewerGesture } from '@/utils/touchGestureManager'
```

## 基本使用

### 创建手势管理器

```javascript
const gestureManager = new TouchGestureManager({
  minScale: 0.5,
  maxScale: 3,
  swipeThreshold: 50,
  maxVerticalDistance: 100,
  doubleTapThreshold: 300
})
```

### 设置手势回调

```javascript
// 滑动手势
gestureManager.on('swipeLeft', (data) => {
  console.log('左滑', data)
})

gestureManager.on('swipeRight', (data) => {
  console.log('右滑', data)
})

gestureManager.on('swipeUp', (data) => {
  console.log('上滑', data)
})

gestureManager.on('swipeDown', (data) => {
  console.log('下滑', data)
})

// 缩放手势
gestureManager.on('scale', (data) => {
  console.log('缩放', data.scale, data.translateX, data.translateY)
})

// 拖拽手势
gestureManager.on('drag', (data) => {
  console.log('拖拽', data.translateX, data.translateY)
})

// 双击手势
gestureManager.on('doubleTap', (data) => {
  console.log('双击', data.x, data.y, data.currentScale)
})

// 单击手势
gestureManager.on('singleTap', (data) => {
  console.log('单击', data.x, data.y)
})
```

### 绑定触摸事件

在Vue组件中：

```vue
<template>
  <div 
    @touchstart="handleTouchStart"
    @touchmove="handleTouchMove" 
    @touchend="handleTouchEnd"
  >
    <!-- 内容 -->
  </div>
</template>

<script>
export default {
  setup() {
    const gestureManager = new TouchGestureManager()
    
    const handleTouchStart = (e) => {
      gestureManager.handleTouchStart(e)
    }
    
    const handleTouchMove = (e) => {
      gestureManager.handleTouchMove(e)
    }
    
    const handleTouchEnd = (e) => {
      gestureManager.handleTouchEnd(e)
    }
    
    // 激活手势管理器
    gestureManager.activate()
    
    return {
      handleTouchStart,
      handleTouchMove,
      handleTouchEnd
    }
  }
}
</script>
```

## API 参考

### 构造函数选项

| 选项 | 类型 | 默认值 | 描述 |
|------|------|--------|------|
| `minScale` | Number | 0.5 | 最小缩放比例 |
| `maxScale` | Number | 3 | 最大缩放比例 |
| `swipeThreshold` | Number | 50 | 滑动手势的最小距离阈值（像素） |
| `maxVerticalDistance` | Number | 100 | 水平滑动时允许的最大垂直偏移 |
| `doubleTapThreshold` | Number | 300 | 双击检测的时间阈值（毫秒） |

### 方法

#### `activate()`
激活手势管理器

#### `deactivate()`
停用手势管理器

#### `reset()`
重置所有状态到初始值

#### `on(event, callback)`
设置事件回调函数

#### `setScale(scale)`
设置缩放比例
- `scale`: Number - 缩放比例

#### `setTranslate(x, y)`
设置平移位置
- `x`: Number - X轴平移距离
- `y`: Number - Y轴平移距离

#### `getTransform()`
获取当前变换状态
- 返回: `{ scale, translateX, translateY }`

### 事件类型

| 事件名称 | 数据参数 | 描述 |
|----------|----------|------|
| `swipeLeft` | `{ deltaX, deltaY }` | 左滑手势 |
| `swipeRight` | `{ deltaX, deltaY }` | 右滑手势 |
| `swipeUp` | `{ deltaX, deltaY }` | 上滑手势 |
| `swipeDown` | `{ deltaX, deltaY }` | 下滑手势 |
| `scale` | `{ scale, translateX, translateY }` | 缩放手势 |
| `drag` | `{ translateX, translateY }` | 拖拽手势 |
| `doubleTap` | `{ x, y, currentScale }` | 双击手势 |
| `singleTap` | `{ x, y }` | 单击手势 |

## 便捷函数

`createPhotoViewerGesture(options)` - 创建适用于照片查看器的手势管理器实例

```javascript
const gestureManager = createPhotoViewerGesture({
  minScale: 0.5,
  maxScale: 3
})
```

## 注意事项

1. 确保在需要时调用 `activate()` 和 `deactivate()` 来控制手势管理器的生命周期
2. 在组件销毁时记得调用 `deactivate()` 避免内存泄漏  
3. 触摸事件需要在支持触摸的设备上才能正常工作
4. 为了防止浏览器默认行为，手势管理器会自动调用 `preventDefault()`

## 测试

可以通过访问 `/touch-test.html` 页面来测试触摸手势功能是否正常工作。
