import { describe, it, expect, beforeEach, jest } from '@jest/globals';
import { registerSearchGameObjectsTool } from '../tools/searchGameObjectsTool.js';
import { registerManageAssetTool } from '../tools/manageAssetTool.js';
import { registerTakeScreenshotTool } from '../tools/takeScreenshotTool.js';
import { ErrorType, McpUnityError } from '../utils/errors.js';

describe('P0 tool registrations', () => {
  const mockLogger = {
    info: jest.fn(),
    debug: jest.fn(),
    warn: jest.fn(),
    error: jest.fn(),
  };

  const mockSendRequest = jest.fn();
  const mockMcpUnity = { sendRequest: mockSendRequest };
  const mockServerTool = jest.fn();
  const mockServer = { tool: mockServerTool };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('registers the new P0 tools', () => {
    registerSearchGameObjectsTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerManageAssetTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerTakeScreenshotTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

    expect(mockServerTool).toHaveBeenCalledWith('search_gameobjects', expect.any(String), expect.any(Object), expect.any(Function));
    expect(mockServerTool).toHaveBeenCalledWith('manage_asset', expect.any(String), expect.any(Object), expect.any(Function));
    expect(mockServerTool).toHaveBeenCalledWith('take_screenshot', expect.any(String), expect.any(Object), expect.any(Function));
  });

  it('validates mutually exclusive parent filters for search_gameobjects', async () => {
    registerSearchGameObjectsTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

    const handler = mockServerTool.mock.calls.find(call => call[0] === 'search_gameobjects')?.[3];
    await expect(handler({ parentId: 1, parentPath: '/Root' })).rejects.toMatchObject<McpUnityError>({
      type: ErrorType.VALIDATION,
      message: "Use either 'parentId' or 'parentPath', not both",
    });
  });

  it('validates required arguments for manage_asset', async () => {
    registerManageAssetTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

    const handler = mockServerTool.mock.calls.find(call => call[0] === 'manage_asset')?.[3];
    await expect(handler({ action: 'move', assetPath: 'Assets/Test.txt' })).rejects.toMatchObject<McpUnityError>({
      type: ErrorType.VALIDATION,
      message: "'move' requires 'assetPath' and 'destinationPath'",
    });
  });

  it('uses scene mode by default for take_screenshot', async () => {
    mockSendRequest.mockResolvedValue({
      success: true,
      message: 'Captured Scene view screenshot.',
      mode: 'scene',
      path: 'C:/temp/scene.png',
      width: 640,
      height: 480,
    });

    registerTakeScreenshotTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

    const handler = mockServerTool.mock.calls.find(call => call[0] === 'take_screenshot')?.[3];
    const result = await handler({});

    expect(mockSendRequest).toHaveBeenCalledWith({
      method: 'take_screenshot',
      params: {
        mode: 'scene',
        width: undefined,
        height: undefined,
        filePath: undefined,
      },
    });

    const payload = JSON.parse(result.content[0].text);
    expect(payload.mode).toBe('scene');
    expect(payload.path).toContain('scene.png');
  });
});