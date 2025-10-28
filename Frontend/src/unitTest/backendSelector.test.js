import { selectBackend,clearBackendCache } from '../utils/backendSelector';

export async function runTestCases() {
  const testCases = [
    {
      name: 'Case 1: 相同优先级，选择更快的',
      endpoints: [
        { url: 'http://127.0.0.1:5099', priority: 1 }, // 正常
        { url: 'http://invalid.localhost:5099', priority: 1 },
        { url: 'http://localhost:5099', priority: 1 }  // 延迟
      ],
      expected: 'http://127.0.0.1:5099'
    },
    {
      name: 'Case 2: 优先级更高但更慢，选择优先级高的',
      endpoints: [
        { url: 'http://127.0.0.1:5099', priority: 3 }, // 正常
        { url: 'http://localhost:5099', priority: 1 }  // 延迟
      ],
      expected: 'http://localhost:5099'
    },
    {
      name: 'Case 3: 所有地址不可访问',
      endpoints: [
        { url: 'http://invalid.localhost:5099', priority: 1 },
        { url: 'http://invalid2.localhost:5099', priority: 2 }
      ],
      expected: null
    },
    {
      name: 'Case 4: 只有一个地址',
      endpoints: [
        { url: 'http://127.0.0.1:5099', priority: 1 }
      ],
      expected: 'http://127.0.0.1:5099'
    }
  ];

  
  for (const testCase of testCases) {
    clearBackendCache(); // 清除缓存
    console.log(`\n🧪 ${testCase.name}`);
    const result = await selectBackend(testCase.endpoints, false);
    console.log(`➡️  Selected: ${result}`);
    console.log(`✅ Expected: ${testCase.expected}`);
    console.log(result === testCase.expected ? '✅ Test Passed' : '❌ Test Failed');
  }

}
