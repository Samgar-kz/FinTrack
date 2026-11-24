import './assets/main.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'
import { installAuth } from './stores/auth'
import { useCatalogStore } from './stores/catalog'

const app = createApp(App)

app.use(createPinia())
installAuth(() => {
  useCatalogStore().reset()
  void router.push({ name: 'login', query: { expired: '1' } })
})
app.use(router)

app.mount('#app')
