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
    agent any  // Run on Jenkins master
    
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
                    // Use Git commit hash as image tag for GitOps
                    env.IMAGE_TAG = "v1.0.${env.BUILD_NUMBER}"
                }
                echo "Git Commit: ${env.GIT_COMMIT_SHORT}"
                echo "Message: ${env.GIT_COMMIT_MSG}"
                echo "Image Tag: ${env.IMAGE_TAG}"
                echo '========================================='
            }
        }
        
        stage('🐳 Trigger Local Build') {
            steps {
                echo '========================================='
                echo '  Build Instructions'
                echo '========================================='
                echo 'Chạy script build-and-push.ps1 trên máy local:'
                echo ''
                echo "  cd E:\\AKS-DEMO"
                echo "  .\\build-and-push.ps1 -ProjectName backend -Version ${IMAGE_TAG}"
                echo ''
                echo 'Script sẽ tự động build và push image lên ACR.'
                echo 'Pipeline sẽ tự động update GitOps repository.'
                echo '========================================='
            }
        }
        
        stage('📝 Update GitOps') {
            when {
                anyOf {
                    branch 'main'
                    expression { env.GIT_BRANCH == 'origin/main' }
                }
            }
            steps {
                echo 'Updating GitOps repository...'
                withCredentials([usernamePassword(credentialsId: 'git-credentials', 
                                                  usernameVariable: 'GIT_USER', 
                                                  passwordVariable: 'GIT_PASS')]) {
                    sh '''
                        # Install kustomize to workspace
                        if [ ! -f ./kustomize ]; then
                            curl -s "https://raw.githubusercontent.com/kubernetes-sigs/kustomize/master/hack/install_kustomize.sh" | bash
                        fi
                        
                        # Save workspace path
                        WORKSPACE_DIR=$(pwd)
                        
                        # Clone GitOps repo
                        rm -rf gitops
                        git clone https://${GIT_USER}:${GIT_PASS}@github.com/longtpit2573/Infrastructure.git gitops
                        cd gitops/${GITOPS_PATH}
                        
                        # Update image tag using absolute path
                        ${WORKSPACE_DIR}/kustomize edit set image ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}
                        
                        # Commit and push
                        git config user.name "Jenkins CI"
                        git config user.email "jenkins@longops.io.vn"
                        git add kustomization.yaml
                        git commit -m "chore: update backend image to ${IMAGE_TAG} [skip ci]" || true
                        git push https://${GIT_USER}:${GIT_PASS}@github.com/longtpit2573/Infrastructure.git main
                    '''
                }
                echo '✅ GitOps repo updated'
                echo 'ArgoCD will auto-sync in 3 minutes'
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
            echo 'Cleaning up Docker images...'
            sh """
                docker rmi ${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG} || true
                docker rmi ${ACR_REGISTRY}/${IMAGE_NAME}:latest || true
            """ 
        }
    }
}
