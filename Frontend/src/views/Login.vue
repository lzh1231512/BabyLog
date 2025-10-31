<template>
  <div class="login-container">
    <form class="login-form" @submit.prevent="handleLogin">
      <h2 class="login-title">登录</h2>
      <input
        v-model="password"
        type="password"
        class="password-input"
        placeholder="请输入密码"
        autocomplete="current-password"
      />
      <button type="submit" class="login-btn">登录</button>
    </form>
  </div>
</template>

<script>
import { ref } from 'vue'
import { login } from '../api/events';
import { useRouter } from 'vue-router'

export default {
  name: 'LoginPage',
  setup() {
    const password = ref('')
    const router = useRouter()
    const handleLogin = async () => {
      localStorage.setItem('APIKEY', password.value);
      const isLoggedIn = await login();
      if (isLoggedIn) {
        // 登录成功，跳转到首页
        router.push({ name: 'HomePage' });
      } else {
        // 登录失败，提示用户
        alert('登录失败，请检查密码');
      }
    }

    return { password, handleLogin }
  }
}
</script>

<style scoped>
.login-container {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
}
.login-form {
  background: #fff;
  padding: 32px 24px;
  border-radius: 16px;
  box-shadow: 0 8px 32px rgba(0,0,0,0.08);
  display: flex;
  flex-direction: column;
  width: 320px;
  max-width: 90vw;
}
.login-title {
  font-size: 22px;
  font-weight: 600;
  margin-bottom: 24px;
  text-align: center;
  color: #2c3e50;
}
.password-input {
  padding: 12px;
  font-size: 16px;
  border-radius: 8px;
  border: 1px solid #bdc3c7;
  margin-bottom: 20px;
  outline: none;
  transition: border-color 0.2s;
}
.password-input:focus {
  border-color: #667eea;
}
.login-btn {
  padding: 12px;
  font-size: 16px;
  border-radius: 8px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: #fff;
  border: none;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.2s;
}
.login-btn:hover {
  background: linear-gradient(135deg, #5a6c7d 0%, #764ba2 100%);
}
@media (max-width: 480px) {
  .login-form {
    padding: 20px 10px;
    width: 95vw;
    min-width: 0;
  }
  .login-title {
    font-size: 18px;
  }
  .password-input, .login-btn {
    font-size: 15px;
    padding: 10px;
  }
}
</style>
