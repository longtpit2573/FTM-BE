// Jenkinsfile for FTM Backend - Full CI/CD Pipeline
// 
// Architecture:
// - Runs on Kubernetes agent pod with .NET SDK, Docker-in-Docker, and kubectl
// - Pod template defined in Jenkins Configuration as Code (JCasC)
// - Multi-stage pipeline: Build → Docker Build → Push to ACR → Update GitOps
// - Triggers ArgoCD auto-sync for deployment to AKS
//
// Prerequisites:
// - Jenkins with Kubernetes plugin
// - Pod template 'backend-builder' configured
// - Credentials: 'acr-credentials', 'git-credentials'

pipeline {
    agent {
        label 'backend-builder'  // References pod template in JCasC
    }
    
    environment {
        // ACR Configuration
        ACR_NAME = 'acrftmbackenddev'
        ACR_REGISTRY = "${ACR_NAME}.azurecr.io"
        IMAGE_NAME = 'ftm-backend'
        IMAGE_TAG = "${env.BUILD_NUMBER}"
        
        // GitOps Configuration
        GITOPS_REPO = 'https://github.com/longtpit2573/Infrastructure.git'
        GITOPS_PATH = 'applications/overlays/dev'
        
        // Credentials
        ACR_CREDENTIALS = credentials('acr-credentials')
        GIT_CREDENTIALS = credentials('git-credentials')
    }
    
    options {
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timeout(time: 30, unit: 'MINUTES')
    }
    
    stages {
        stage('📋 Checkout') {
            steps {
                container('dotnet') {
                    echo '========================================='
                    echo '  FTM Backend CI/CD Pipeline'
                    echo '========================================='
                    checkout scm
                    script {
                        env.GIT_COMMIT_SHORT = sh(
                            script: "git rev-parse --short HEAD",
                            returnStdout: true
                        ).trim()
                        env.GIT_COMMIT_MSG = sh(
                            script: 'git log -1 --pretty=%B',
                            returnStdout: true
                        ).trim()
                    }
                    echo "Git Commit: ${env.GIT_COMMIT_SHORT}"
                    echo "Message: ${env.GIT_COMMIT_MSG}"
                    echo "Image: ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}"
                    echo '========================================='
                }
            }
        }
        
        stage('🔧 Build .NET') {
            steps {
                container('dotnet') {
                    echo 'Building .NET project...'
                    dir('FTM-BE') {
                        sh '''
                            dotnet restore FTM.sln
                            dotnet build FTM.sln --configuration Release --no-restore
                        '''
                    }
                    echo '✅ Build completed'
                }
            }
        }
        
        stage('🐳 Docker Build & Push') {
            steps {
                container('docker') {
                    echo 'Building and pushing Docker image...'
                    dir('FTM-BE') {
                        sh """
                            # Wait for Docker daemon
                            timeout 30 sh -c 'until docker info; do sleep 1; done' || true
                            
                            # Build image
                            docker build -t ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG} .
                            docker tag ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG} ${ACR_REGISTRY}/${IMAGE_NAME}:latest
                            
                            # Login to ACR
                            echo ${ACR_CREDENTIALS_PSW} | docker login ${ACR_REGISTRY} \
                                --username ${ACR_CREDENTIALS_USR} \
                                --password-stdin
                            
                            # Push images
                            docker push ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}
                            docker push ${ACR_REGISTRY}/${IMAGE_NAME}:latest
                        """
                    }
                    echo '✅ Images pushed to ACR'
                }
            }
        }
        
        stage('📝 Update GitOps') {
            when {
                branch 'main'
            }
            steps {
                container('kubectl') {
                    echo 'Updating GitOps repository...'
                    withCredentials([usernamePassword(credentialsId: 'git-credentials', 
                                                      usernameVariable: 'GIT_USER', 
                                                      passwordVariable: 'GIT_PASS')]) {
                        sh """
                            # Install git and kustomize
                            apt-get update && apt-get install -y git curl
                            curl -s "https://raw.githubusercontent.com/kubernetes-sigs/kustomize/master/hack/install_kustomize.sh" | bash
                            mv kustomize /usr/local/bin/
                            
                            # Clone GitOps repo
                            git clone https://${GIT_USER}:${GIT_PASS}@github.com/longtpit2573/Infrastructure.git gitops
                            cd gitops/${GITOPS_PATH}
                            
                            # Update image tag
                            kustomize edit set image ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}
                            
                            # Commit and push
                            git config user.name "Jenkins CI"
                            git config user.email "jenkins@longops.io.vn"
                            git add kustomization.yaml
                            git commit -m "chore: update backend image to ${IMAGE_TAG} [skip ci]" || true
                            git push https://${GIT_USER}:${GIT_PASS}@github.com/longtpit2573/Infrastructure.git main
                        """
                    }
                    echo '✅ GitOps repo updated'
                    echo 'ArgoCD will auto-sync in 3 minutes'
                }
            }
        }
    }
    
    post {
        success {
            echo '========================================='
            echo '  ✅ CI/CD PIPELINE SUCCESS'
            echo '========================================='
            echo "Image: ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}"
            echo "Commit: ${env.GIT_COMMIT_SHORT}"
            echo "Message: ${env.GIT_COMMIT_MSG}"
            echo ''
            echo 'Next: ArgoCD will deploy to AKS in ~3 minutes'
            echo '========================================='
        }
        failure {
            echo '========================================='
            echo '  ❌ PIPELINE FAILED'
            echo '========================================='
            echo "Build: ${env.BUILD_NUMBER}"
            echo "Check logs: ${env.BUILD_URL}"
            echo '========================================='
        }
        always {
            container('docker') {
                echo 'Cleaning up Docker images...'
                sh """
                    docker rmi ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG} || true
                    docker rmi ${ACR_REGISTRY}/${IMAGE_NAME}:latest || true
                """ 
            }
        }
    }
}
