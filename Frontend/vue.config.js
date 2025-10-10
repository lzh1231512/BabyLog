const { defineConfig } = require('@vue/cli-service')

module.exports = defineConfig({
  transpileDependencies: true,
  
  // 开发环境配置
  configureWebpack: config => {
    if (process.env.NODE_ENV === 'development') {
      // 使用 cheap-module-source-map 以获得更好的调试体验
      config.devtool = 'cheap-module-source-map'
      
      // 禁用代码压缩以便调试
      config.optimization = {
        ...config.optimization,
        minimize: false
      }
    }
  },
  
  css: {
    sourceMap: true
  },
  
  devServer: {
    port: 8081,
    host: 'localhost',
    https: false,
    hot: true,
    open: false,
    // 代理配置（如果需要）
    proxy: {
      '/api': {
        target: 'http://localhost:5099',
        changeOrigin: true
      }
    }
  }
})
