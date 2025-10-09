
let configData = null;
export async function loadConfig() {
  if (configData) return configData;

  const maxDepth = 8; 
  let path = './';
  for (let i = 0; i < maxDepth; i++) {
    try {
      const response = await fetch(`${path}config.json`);
      if (response.ok) {
        configData = await response.json();
        return configData;
      }
    } catch (e) {
      // 忽略错误，继续尝试上一级目录
    }
    path = '../' + path;
  }
  throw new Error('配置文件 config.json 加载失败');
}

export default {
  get API_BASE_URL() {
    return configData ? configData.API_BASE_URL : '';
  },
  get BasePath() {
    return configData ? configData.BasePath : '';
  }
};
