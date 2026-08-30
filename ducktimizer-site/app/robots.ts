import type { MetadataRoute } from 'next';

export default function robots(): MetadataRoute.Robots {
  return { rules: { userAgent: '*', allow: '/', disallow: ['/admin', '/api/'] }, sitemap: 'https://fluffy-octo-palm-tree.guilhermedasilvabertoso.workers.dev/sitemap.xml' };
}
