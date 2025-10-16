<template>
  <div>
    <h1>Video Test 1 - HTML Video Tag (downloadVideo API)</h1>
    <video controls width="800" height="600">
      <source src="/api/Files/downloadVideo?id=2&fileName=20251015082452089_h1dpm5.mp4" type="video/mp4">
      Your browser does not support the video tag.
    </video>
  </div>
</template>

<script>
export default {
  name: 'VideoTest1'
}
</script>
